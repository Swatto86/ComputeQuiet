import { remote } from 'webdriverio';
import { spawn, spawnSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, readFileSync, existsSync, writeFileSync, unlinkSync } from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import assert from 'node:assert/strict';
import { setTimeout as sleep } from 'node:timers/promises';

const root=path.resolve(import.meta.dirname,'..');
const live=process.argv.includes('--live');
const binary=process.env.GAMEQUIET_BINARY || path.join(root,'target','debug','gamequiet.exe');
const temp=mkdtempSync(path.join(os.tmpdir(),'gamequiet-e2e-'));
const state=path.join(temp,'state');mkdirSync(state);
const probeLog=path.join(temp,'probe.log');
const probe=path.join(temp,'GqProbe.exe');
const fakeCli=path.join(temp,'FakeCli.exe');
const compiler=path.join(process.env.WINDIR,'Microsoft.NET','Framework64','v4.0.30319','csc.exe');
for(const [source,out,refs] of [['Probe.cs',probe,['System.Windows.Forms.dll','System.Drawing.dll']],['FakeCli.cs',fakeCli,['System.Web.Extensions.dll']]]) {
  const compiled=spawnSync(compiler,['/nologo',`/target:${source==='Probe.cs'?'winexe':'exe'}`,`/out:${out}`,...refs.map(r=>`/reference:${r}`),path.join(root,'tests',source)],{encoding:'utf8',windowsHide:true});
  assert.equal(compiled.status,0,compiled.stdout+compiled.stderr);
}
const env={...process.env,GAMEQUIET_DATA_DIR:state,GAMEQUIET_TEST_ROOT:temp,GAMEQUIET_PROBE_LOG:probeLog};
const fixture=spawn(probe,[],{env,stdio:'ignore',windowsHide:false});
const edge=process.env.MSEDGEDRIVER || path.join(process.env.LOCALAPPDATA,'GameQuietTools','msedgedriver.exe');
assert.ok(existsSync(edge),'Set MSEDGEDRIVER to the driver matching WebView2');
const driver=spawn('tauri-driver',['--native-driver',edge,'--port','4455'],{env,stdio:['ignore','pipe','pipe'],windowsHide:true});
let logs='';driver.stdout.on('data',b=>logs+=b);driver.stderr.on('data',b=>logs+=b);
let browser;
async function connect(){
  for(let i=0;i<40;i++) {try {await fetch('http://127.0.0.1:4455/status');break;} catch {await sleep(250);}}
  browser=await remote({hostname:'127.0.0.1',port:4455,logLevel:'error',capabilities:{'tauri:options':{application:binary},'wdio:enforceWebDriverClassic':true},connectionRetryCount:0});
  await browser.$('#toggle').waitForDisplayed({timeout:30000});
}
async function idle(timeout=180000){await browser.waitUntil(async()=>!(await browser.$('#toggle').getAttribute('disabled')),{timeout,timeoutMsg:'UI remained busy'});}
async function click(selector){await browser.$(selector).click();await idle();}
async function tab(name){await browser.$(`[data-tab="${name}"]`).click();}
async function appExit(){
  await tab('settings');
  await browser.$('#quit').click();
  await sleep(1000);
  await browser.deleteSession().catch(()=>{});browser=null;
}
try {
  await connect();
  assert.equal(await browser.$('#mode').getText(),'READY');
  await tab('settings');
  if(live&&process.argv.includes('--claude')) {await browser.$('#provider').selectByAttribute('value','claude');await browser.$('#model').setValue('haiku');}
  if(!live)await browser.$('#cli-path').setValue(fakeCli);
  await click('#save');
  assert.ok(existsSync(path.join(state,'state.json')),'Settings persisted');
  await tab('workloads');
  await click('#scan');
  const names=await browser.$$('#rows h4').map(el=>el.getText());assert.ok(names.includes('GqProbe'),'Real disposable app appears in scan');
  const snapshot=await browser.execute(()=>window.__TAURI__.core.invoke('get_state'));
  await browser.setWindowSize(780,760);
  const layout=await browser.execute(()=>{
    const selects=[...document.querySelectorAll('#rows select')];
    const canvas=document.createElement('canvas').getContext('2d');
    return {
      count:selects.length,
      fits:selects.every(el=>{const s=getComputedStyle(el);canvas.font=s.font;return Math.max(...[...el.options].map(o=>canvas.measureText(o.text).width))+parseFloat(s.paddingLeft)+parseFloat(s.paddingRight)+24<=el.clientWidth;}),
      overflow:document.documentElement.scrollWidth>innerWidth,
      order:[...document.querySelectorAll('#rows .row')].map(row=>{const value=row.querySelector('select')?.value;return value==='ask'?0:value==='allow'?1:2;})
    };
  });
  assert.ok(layout.count>0,'Layout check includes real dropdowns');
  assert.ok(layout.fits&&!layout.overflow,`Dropdown labels fit at minimum window width: ${JSON.stringify(layout)}`);
  assert.deepEqual(layout.order,[...layout.order].sort((a,b)=>a-b),'Review cards precede close cards and kept-running cards');
  await browser.$('select[aria-label="Preference for GqProbe"]').scrollIntoView();
  await browser.saveScreenshot(path.join(temp,'narrow-workloads.png'));
  await browser.setWindowSize(1080,760);
  for(const w of snapshot.snapshot.workloads)assert.ok(w.cpu>=0&&w.cpu<=100,`CPU out of range for ${w.name}`);
  const protectedApp=snapshot.snapshot.workloads.find(w=>w.name.toLowerCase()==='gamequiet');
  assert.ok(protectedApp?.blocked,'App protects itself');
  const denial=await browser.execute(async id=>{try{await window.__TAURI__.core.invoke('set_preference',{id,preference:'allow'});return false;}catch{return true;}},protectedApp.id);
  assert.ok(denial,'Rust rejects approval for a protected workload');
  await click('#assess');
  const status=await browser.$('#status').getText();
  assert.match(status,/assessment complete/i,`Provider failed: ${status}`);
  if(!live)assert.ok(readFileSync(path.join(temp,'provider-cwd.txt'),'utf8').startsWith(state+path.sep+'assessment-'),'Provider runs in its isolated scratch directory');
  await browser.saveScreenshot(path.join(temp,'assessment.png'));
  let select=await browser.$('select[aria-label="Preference for GqProbe"]');
  await select.selectByVisibleText('Close for session');await browser.$('#confirm').waitForDisplayed();await click('#accept');
  await click('#toggle');
  assert.equal(await browser.$('#mode').getText(),'QUIET SESSION');
  assert.ok(await browser.$('#assess').getAttribute('disabled'),'Cloud assessment unavailable during session');
  assert.match(readFileSync(probeLog,'utf8'),/closed:/,'OS accepted normal close');
  const journal=JSON.parse(readFileSync(path.join(state,'state.json'),'utf8'));
  assert.equal(journal.recovery.length,1);assert.equal(journal.recovery[0].status,'stopped');
  const listed=await browser.$$('#rows h4').map(el=>el.getText());
  assert.ok(listed.length>0,'Rows still render once Game Mode is on, so the check below cannot pass vacuously');
  assert.ok(!listed.includes('GqProbe'),`A stopped app must leave the list: ${JSON.stringify(listed)}`);
  assert.match(await browser.$('#count').getText(),/Snapshot .*not live/,'Measurements are timestamped and never described as live');
  await click('#scan');
  assert.match(await browser.$('#count').getText(),/Snapshot .*not live/);
  assert.match(await browser.$('#status').getText(),/1 confirmed stop/,'Scanning preserves the session outcome');
  assert.equal(await browser.$('#summary').isDisplayed(),false,'A new snapshot invalidates the previous cloud summary');
  const running=await browser.execute(()=>window.__TAURI__.core.invoke('get_state'));
  process.kill(running.process_id); // Only the app started by this isolated WebDriver session.
  await browser.deleteSession().catch(()=>{});browser=null;
  await connect();
  assert.equal(await browser.$('#mode').getText(),'QUIET SESSION','Recovery survives forced termination');
  await click('#toggle');
  assert.equal(await browser.$('#mode').getText(),'READY');
  assert.equal((await browser.$$('#rows .row')).length,0,'Restoring invalidates old measurements');
  assert.equal(JSON.parse(readFileSync(path.join(state,'state.json'),'utf8')).recovery.length,0);
  assert.ok(readFileSync(probeLog,'utf8').split('started:').length>=3,'App was relaunched');
  // A real app refuses the normal close request, as with an unsaved document.
  writeFileSync(probeLog+'.refuse','refuse');
  await click('#toggle');
  assert.match(readFileSync(probeLog,'utf8'),/refused:/,'The refusal path actually ran');
  assert.equal(await browser.$('#mode').getText(),'NEEDS ATTENTION','A failed stop must not advertise success');
  assert.match(await browser.$('#description').getText(),/0 confirmed stop/);
  assert.match(await browser.$('#rows').getText(),/ACTION UNCONFIRMED/);
  assert.ok(await browser.$('select[aria-label="Preference for GqProbe"]').getAttribute('disabled'));
  await click('#scan');
  assert.match(await browser.$('#status').getText(),/0 confirmed stops, 1 unconfirmed/);
  await tab('history');
  await browser.$('#recovery button').click();await click('#accept');
  assert.equal(await browser.$('#mode').getText(),'READY');
  assert.match(await browser.$('#status').getText(),/Session ended/);
  unlinkSync(probeLog+'.refuse');
  await tab('workloads');
  // Close-to-tray must keep the process and state alive.
  await browser.$('#hide').click();
  await sleep(400);
  await browser.execute(()=>window.__TAURI__.window.getCurrentWindow().show());
  await browser.$('#toggle').waitForDisplayed();
  await browser.saveScreenshot(path.join(temp,'main.png'));
  await appExit();
  await connect();
  const persisted=await browser.execute(()=>window.__TAURI__.core.invoke('get_state'));
  assert.ok(persisted.state.rules.some(r=>r.preference==='allow'),'Preference survives a real app restart');
  assert.ok(persisted.state.history.some(h=>h.includes('restored')),'History survives restart');
  await appExit();
  console.log(`PASS: ${live?'real cloud':'fixture cloud'} assessment, real process close/restore, IPC protection, close-to-tray, persistence and clean exit. Evidence: ${temp}`);
} catch(error) {
  if(browser){await browser.saveScreenshot(path.join(temp,'failure.png')).catch(()=>{});writeFileSync(path.join(temp,'page.html'),await browser.getPageSource().catch(()=>''));}
  writeFileSync(path.join(temp,'driver.log'),logs);
  console.error(`Evidence: ${temp}`);throw error;
} finally {
  if(browser)await browser.deleteSession().catch(()=>{});
  driver.kill();
  // Only fixture PIDs recorded by this run; never terminate by name.
  if(existsSync(probeLog))for(const match of readFileSync(probeLog,'utf8').matchAll(/started:(\d+)/g)) {
    spawnSync('powershell.exe',['-NoProfile','-Command',`$p=Get-Process -Id ${Number(match[1])} -ErrorAction SilentlyContinue; if ($p -and $p.Path -eq '${probe.replaceAll("'","''")}') { Stop-Process -Id $p.Id }`],{windowsHide:true});
  }
  fixture.kill();
}
