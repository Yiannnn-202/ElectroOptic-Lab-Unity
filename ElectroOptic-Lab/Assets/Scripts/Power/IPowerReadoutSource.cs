public interface IPowerReadoutSource
{
    float CurrentStablePower { get; }
    float CurrentDisplayPower { get; }
    float CurrentAlignmentEfficiency { get; }
}
