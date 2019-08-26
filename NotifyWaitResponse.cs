namespace crozone.LinuxGpio
{
    public struct NotifyWaitResponse
    {
        public string WatchedFilename { get; set; }
        public string[] EventNames { get; set; }
        public string EventFilename { get; set; }
    }
}
