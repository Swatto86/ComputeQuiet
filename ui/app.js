const { invoke } = window.__TAURI__.core;
const $ = id => document.getElementById(id);
let view, busy = false;
function text(tag, value, className) { const el=document.createElement(tag); el.textContent=value; if(className)el.className=className; return el; }
function preference(w) { return view.state.rules.find(r=>r.key===`${w.kind}|${w.exe.toLowerCase()}|${w.hash}`)?.preference || 'ask'; }
function status(message,error=false) { $('status').textContent=message; $('status').className=error?'error':busy?'busy':''; }
function lock(value) {
  busy=value;const working=busy||view?.busy, active=view?.state.active;
  document.querySelectorAll('button:not(.tab),select,input').forEach(el=>el.disabled=!!working);
  document.querySelectorAll('#rows select,#settings input,#settings select,#save,#assess').forEach(el=>el.disabled=!!(working||active));
  $('restore').disabled=!!working||!active;
}
async function run(command,args={},message='Working…') {
  if(busy||view?.busy)return;
  lock(true);status(message);
  try { const result=await invoke(command,args); if(result)render(result); }
  catch(error) { status(String(error),true); }
  finally { lock(false); $('status').classList.remove('busy'); }
}
function confirm(title,copy,action) {
  $('confirm-title').textContent=title; $('confirm-copy').textContent=copy;
  $('accept').onclick=()=>{ $('confirm').close();action(); };
  $('confirm').showModal();
}
function render(next) {
  view=next;const active=view.state.active;
  const stopped=view.state.recovery.filter(r=>r.status==='stopped').length;
  const attention=view.state.recovery.length-stopped;
  $('mode').textContent=view.busy?'WORKING':view.session_label;
  $('mode').classList.toggle('on',active&&!attention&&stopped>0);
  $('mode').classList.toggle('attention',active&&(attention>0||!stopped));
  $('headline').textContent=active?(attention||!stopped?'Your session needs attention.':'Your quiet session is saved.'):'Make room for demanding work.';
  $('description').textContent=active?`${stopped} confirmed stop(s) · ${attention} unconfirmed or failed action(s). These are recorded results, not live process status. Restore to finish, or scan to check what is running now.`:'Free resources for games, rendering, builds, video editing or local AI. Keep your main workload running; close only the background apps you approve.';
  $('toggle').textContent=active?'End session & restore':'Start quiet session';
  status(view.status);
  $('provider').value=view.state.settings.provider;$('model').value=view.state.settings.model;
  $('cli-path').value=view.state.settings.cli_path||'';
  $('interrupt').checked=view.state.settings.interrupt_ollama;
  $('count').textContent=view.sampled_at?`${view.snapshot.workloads.length} workloads · ${view.snapshot.workloads.filter(w=>!w.blocked).length} with a supported restore path · Snapshot ${new Date(view.sampled_at*1000).toLocaleTimeString()} — not live`:'Scan for a fresh snapshot of your PC.';
  $('summary').hidden=!view.summary;$('summary').textContent=view.summary;
  $('warnings').replaceChildren(...view.snapshot.warnings.map(w=>text('p',w,'footnote')));
  $('empty').hidden=view.snapshot.workloads.length>0;
  $('empty').querySelector('h3').textContent=view.sampled_at?'No workloads in this snapshot':'Take a fresh snapshot';
  $('empty').querySelector('p').textContent='Scan PC measures CPU, GPU, memory and I/O. Scanning never closes apps.';
  const priority=w=>w.blocked||preference(w)==='keep'?2:preference(w)==='ask'?0:1;
  $('rows').replaceChildren(...view.snapshot.workloads.toSorted((a,b)=>priority(a)-priority(b)).map(w=>{
    const row=text('article','','row'+(w.blocked?' protected':''));row.dataset.id=w.id;
    const label=text('div','');label.append(text('h4',w.name),text('small',w.product||w.publisher||'Unknown publisher'));row.append(label);
    const metrics=text('div','','metrics');
    const unavailable=kind=>view.snapshot.warnings.some(warning=>warning.includes(kind)&&warning.includes('unavailable'));
    for(const [value,name] of [[`${w.cpu.toFixed(1)}%`,'CPU'],[unavailable('GPU')?'—':`${w.gpu.toFixed(0)}%`,'GPU'],[`${Math.round(w.memory_mb)}`,'MB RAM'],[unavailable('I/O')?'—':w.io_mb.toFixed(1),'MB/s I/O']]) {const m=text('div','','metric');m.append(text('strong',value),text('span',name));metrics.append(m);}row.append(metrics);
    if(w.blocked) row.append(text('span','PROTECTED','protected-label'));
    else {
      const select=document.createElement('select');select.setAttribute('aria-label',`Preference for ${w.name}`);select.dataset.preference=w.id;
      for(const [value,label] of [['ask','Review first'],['allow','Close for session'],['keep','Always keep']]) {const option=text('option',label);option.value=value;select.append(option);}
      select.value=preference(w);select.onchange=()=>{const value=select.value;select.value=preference(w);const apply=()=>run('set_preference',{id:w.id,preference:value},'Saving preference…');if(value==='allow')confirm(`Close ${w.name} for a quiet session?`,w.kind==='ollama'?'A quiet session will stop Ollama and interrupt any active generation. Restoration restarts the server and reloads models, but cannot recover an interrupted request. You must also enable Ollama interruption in Settings.':'A quiet session will ask this app to close normally. Save prompts are respected. Restoration launches its executable; restoring documents, tabs and sessions depends on the app itself.',apply);else apply();};row.append(select);
    }
    const advice=view.advice.find(a=>a.id===w.id);const reason=text('div','','reason');
    const failed=view.state.recovery.find(r=>r.workload.id===w.id&&r.status!=='stopped');
    if(failed)reason.append(text('em','ACTION UNCONFIRMED'),text('span',failed.error||'Recovery was saved, but the action did not finish.'));
    else if(advice)reason.append(text('em',`${advice.recommendation.toUpperCase()} · ${advice.confidence}%`),text('span',advice.reason));
    else reason.textContent=w.blocked || (w.kind==='ollama'?`${w.models.length} loaded model(s). Stops local inference and releases GPU memory.`:'Normal close only. Save prompts are respected; no force termination.');
    if(w.blocked&&advice)reason.append(text('div',w.blocked));
    row.append(reason);return row;
  }));
  $('recovery').replaceChildren(...view.state.recovery.map(r=>{
    const el=text('article','','recovery-item');el.append(text('h4',`${r.workload.name} · ${r.status.replaceAll('_',' ')}`),text('p',r.error||'Restore details are safely recorded.'));
    const button=text('button','I restored this manually');button.onclick=()=>confirm('Clear this recovery entry?','Confirm only after you have restored this workload yourself. ComputeQuiet will stop trying to restore this entry.',()=>run('confirm_restored',{id:r.workload.id}));el.append(button);return el;
  }));
  if(!view.state.recovery.length)$('recovery').append(text('p','Nothing waiting to be restored.','footnote'));
  $('activity').replaceChildren(...view.state.history.map(line=>{const split=line.indexOf(' | ');const at=new Date(Number(line.slice(0,split))*1000);return text('li',`${at.toLocaleString()} — ${line.slice(split+3)}`);}));
  lock(busy);
}
document.querySelectorAll('[data-tab]').forEach(button=>button.onclick=()=>{document.querySelectorAll('[data-tab]').forEach(b=>b.classList.toggle('selected',b===button));for(const name of ['workloads','settings','history'])$(name).hidden=name!==button.dataset.tab;});
$('cancel').onclick=()=>$('confirm').close();
$('hide').onclick=()=>window.__TAURI__.window.getCurrentWindow().close();
$('scan').onclick=()=>run('scan',{},'Sampling CPU, GPU, memory and I/O…');
$('assess').onclick=()=>run('assess',{},'Taking a fresh snapshot and asking your cloud CLI. This can take a few minutes…');
$('toggle').onclick=()=>run(view?.state.active?'restore':'enable',{},view?.state.active?'Restoring your workloads…':'Rechecking identities and saving recovery before starting a quiet session…');
$('restore').onclick=()=>run('restore',{},'Restoring workloads…');
$('save').onclick=()=>run('save_settings',{settings:{provider:$('provider').value,model:$('model').value.trim(),cli_path:$('cli-path').value.trim(),interrupt_ollama:$('interrupt').checked}},'Saving settings…');
$('quit').onclick=()=>run('exit_app',{},'Restoring and quitting…');
window.__TAURI__.event.listen('updated',event=>render(event.payload))
  .then(()=>invoke('get_state')).then(render).catch(error=>status(String(error),true))
  .finally(()=>invoke('ready').catch(error=>status(String(error),true)));
