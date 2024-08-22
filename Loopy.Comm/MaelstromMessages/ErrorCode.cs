namespace Loopy.Comm.MaelstromMessages;

public enum ErrorCode
{
    /// <summary>
    /// Indicates that the requested operation could not be completed within a timeout.
    /// </summary>
    Timeout = 0,

    /// <summary>
    /// Thrown when a client sends an RPC request to a node which does not exist.
    /// </summary>
    /// <remarks>Definite: requested operation definitely did not happen</remarks>
    NodeNotFound = 1,

    /// <summary>
    /// Use this error to indicate that a requested operation is not supported by the current implementation.
    /// </summary>
    /// <remarks>Definite: requested operation definitely did not happen</remarks>
    NotSupported = 10,

    /// <summary>
    /// Indicates that the operation definitely cannot be performed at this time.
    /// </summary>
    /// <remarks>Definite: requested operation definitely did not happen</remarks>
    TemporarilyUnavailable = 11,

    /// <summary>
    /// The client's request did not conform to the server's expectations, and could not possibly have been processed.
    /// </summary>
    /// <remarks>Definite: requested operation definitely did not happen</remarks>
    MalformedRequest = 12,

    /// <summary>
    /// Indicates that some kind of general, indefinite error occurred.
    /// </summary>
    /// <remarks>Indefinite: requested operation may have happened</remarks>
    Crash = 13,

    /// <summary>
    /// Indicates that some kind of general, definite error occurred. 
    /// </summary>
    /// <remarks>Definite: requested operation definitely did not happen</remarks> 
    Abort = 14,

    /// <summary>
    /// The client requested an operation on a key which does not exist
    /// (assuming the operation should not automatically create missing keys).
    /// </summary>
    /// <remarks>Definite: requested operation definitely did not happen</remarks> 
    KeyDoesNotExist = 20,

    /// <summary>
    /// The client requested the creation of a key which already exists,
    /// and the server will not overwrite it.
    /// </summary>
    /// <remarks>Definite: requested operation definitely did not happen</remarks> 
    KeyAlreadyExists = 21,

    /// <summary>
    /// The requested operation expected some conditions to hold, and those conditions were not met.
    /// </summary>
    /// <remarks>Definite: requested operation definitely did not happen</remarks> 
    PreconditionFailed = 22,
}
