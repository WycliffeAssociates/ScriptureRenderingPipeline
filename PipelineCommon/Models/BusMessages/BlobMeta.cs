using System;

namespace PipelineCommon.Models.BusMessages;

public class BlobMeta
{
  public string Sha256 { get; set; }
  public string Url { get; set; }
  public long? ByteCount { get; set; }
  public string TimeRendered { get; set; }

  public bool BlobDoesRepresentWholeRepo { get; set; }

  public string FileType { get; set; }
}
public class BlobMetaScripture : BlobMeta
{
  public string ChapterNum { get; set; }

  public string Slug { get; set; }

  public string BookTitle { get; set; }
}