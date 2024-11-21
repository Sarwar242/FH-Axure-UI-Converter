namespace Core.Models
{
    public class AmountToWordResult
    {
        public string Output { get; set; }
        public bool ShouldSkip { get; set; }
        public string PreviousControlId { get; set; }
        public bool IsValid => !string.IsNullOrEmpty(PreviousControlId);
        public ControlInfo PreviousControl { get; set; }
        public ControlInfo CurrentControl { get; set; }
    }
}
