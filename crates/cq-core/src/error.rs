//! One error type for the domain. Every variant carries the context a user
//! needs to act on it — which file, which value — rather than a bare cause.

#[derive(Debug, thiserror::Error)]
pub enum CoreError {
    #[error("{context}: {source}")]
    Io {
        context: String,
        #[source]
        source: std::io::Error,
    },
    #[error("{context}: {source}")]
    Json {
        context: String,
        #[source]
        source: serde_json::Error,
    },
    #[error("{0}")]
    Invalid(String),
    #[error("no configuration directory is available on this system")]
    NoDataDir,
}

impl CoreError {
    pub fn io(context: impl Into<String>, source: std::io::Error) -> Self {
        Self::Io {
            context: context.into(),
            source,
        }
    }

    pub fn json(context: impl Into<String>, source: serde_json::Error) -> Self {
        Self::Json {
            context: context.into(),
            source,
        }
    }
}
