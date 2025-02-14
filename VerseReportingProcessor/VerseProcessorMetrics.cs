using System.Diagnostics.Metrics;

namespace VerseReportingProcessor;

public class VerseProcessorMetrics
{
    private readonly Counter<int> _reposProcessed;
    public VerseProcessorMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(nameof(VerseCounterService));
        _reposProcessed = meter.CreateCounter<int>("wa.verseprocessor.reposprocessed");
    }

    public void ReposProcessed(int value)
    {
        _reposProcessed.Add(value);
    }
}