use serde::{Deserialize, Serialize};

#[derive(Clone, Default, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Workload {
    pub id: String,
    pub name: String,
    pub exe: String,
    pub hash: String,
    pub pid: u32,
    pub started: String,
    pub kind: String,
    pub product: String,
    pub publisher: String,
    pub cpu: f64,
    pub memory_mb: f64,
    pub io_mb: f64,
    pub gpu: f64,
    pub has_window: bool,
    pub blocked: String,
    #[serde(default)]
    pub targets: Vec<Target>,
    #[serde(default)]
    pub models: Vec<LoadedModel>,
}

#[derive(Clone, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Target {
    pub pid: u32,
    pub started: String,
    pub exe: String,
    pub hash: String,
}

#[derive(Clone, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct LoadedModel {
    pub name: String,
    pub context_length: u64,
    pub keep_alive_seconds: i64,
}

impl Workload {
    pub fn key(&self) -> String {
        format!("{}|{}|{}", self.kind, self.exe.to_lowercase(), self.hash)
    }
    pub fn actionable(&self) -> bool {
        self.blocked.is_empty()
            && !self.hash.is_empty()
            && matches!(self.kind.as_str(), "ollama" | "close")
    }
}

#[derive(Clone, Default, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Snapshot {
    pub workloads: Vec<Workload>,
    pub warnings: Vec<String>,
    pub gpu_summary: String,
}

#[derive(Clone, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Advice {
    pub id: String,
    pub recommendation: Recommendation,
    pub confidence: u8,
    pub reason: String,
}

#[derive(Clone, Deserialize, Serialize, PartialEq)]
#[serde(rename_all = "snake_case")]
pub enum Recommendation {
    Close,
    Keep,
    Ask,
}

#[derive(Clone, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Assessment {
    pub summary: String,
    pub workloads: Vec<Advice>,
}

#[derive(Clone, Default, Deserialize, Serialize, PartialEq)]
#[serde(rename_all = "snake_case")]
pub enum Preference {
    Allow,
    #[default]
    Ask,
    Keep,
}

#[derive(Clone, Deserialize, Serialize)]
pub struct Rule {
    pub key: String,
    pub preference: Preference,
}

#[derive(Clone, Deserialize, Serialize)]
pub struct SavedAdvice {
    pub key: String,
    pub at: u64,
    pub advice: Advice,
}

#[derive(Clone, Default, Deserialize, Serialize)]
#[serde(rename_all = "snake_case")]
pub enum Provider {
    #[default]
    Codex,
    Claude,
}

#[derive(Clone, Default, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct Settings {
    pub provider: Provider,
    pub model: String,
    #[serde(default)]
    pub cli_path: String,
    pub automatic: bool,
    pub interrupt_ollama: bool,
}

#[derive(Clone, Deserialize, Serialize)]
pub struct Recovery {
    pub workload: Workload,
    pub status: String,
    pub error: String,
}

#[derive(Clone, Deserialize, Serialize)]
#[serde(deny_unknown_fields)]
pub struct DiskState {
    pub version: u8,
    pub settings: Settings,
    pub rules: Vec<Rule>,
    pub cache: Vec<SavedAdvice>,
    pub active: bool,
    pub recovery: Vec<Recovery>,
    pub history: Vec<String>,
}

impl Default for DiskState {
    fn default() -> Self {
        Self {
            version: 1,
            settings: Settings::default(),
            rules: vec![],
            cache: vec![],
            active: false,
            recovery: vec![],
            history: vec![],
        }
    }
}

#[derive(Clone, Serialize)]
pub struct View {
    pub process_id: u32,
    pub state: DiskState,
    pub snapshot: Snapshot,
    pub advice: Vec<Advice>,
    pub summary: String,
    pub busy: bool,
    pub status: String,
}
