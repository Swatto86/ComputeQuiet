//! ComputeQuiet's domain, with no operating-system calls in it.
//!
//! Everything here is testable without a window or a real process: what a
//! profile contains, which processes must never be touched, how a snapshot of
//! the machine turns into a plan, how executed steps are journaled so they can
//! be undone in reverse, and how settings persist.

pub mod error;
pub mod journal;
pub mod plan;
pub mod policy;
pub mod profile;
pub mod settings;
pub mod snapshot;
pub mod store;

pub use error::CoreError;
pub use journal::{DoneStep, Journal, RestoreStep};
pub use plan::{Capabilities, Plan, Skipped, Step, build_plan};
pub use profile::{Os, PowerPolicy, ProcessAction, ProcessTarget, Profile, ServiceTarget};
pub use settings::{Settings, Theme};
pub use snapshot::{PowerPlan, ProcessInfo, ServiceInfo, ServiceState, Snapshot, SystemStats};
