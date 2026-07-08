namespace VSLoader.Services;

internal enum AdminUiAutoPasteStage
{
    WaitingForDialog,
    BeforePaste,
    PasteSent,
    BeforeEnter,
    EnterSent,
    Completed,
    Aborted
}
