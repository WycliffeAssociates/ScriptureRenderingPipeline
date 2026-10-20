using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace SRPTests.TestHelpers;

/// <summary>
/// An in memory container that lists the blobs it was seeded with and records what gets uploaded
/// </summary>
public class FakeBlobContainerClient : BlobContainerClient
{
    public List<BlobItem> ExistingBlobs { get; } = new();
    public ConcurrentDictionary<string, BlobUploadOptions> Uploads { get; } = new();
    public bool ThrowOnList { get; set; }
    public string? ListedPrefix { get; private set; }

    public void AddExisting(string name, string contentType, byte[]? contentHash)
    {
        ExistingBlobs.Add(BlobsModelFactory.BlobItem(name: name,
            properties: BlobsModelFactory.BlobItemProperties(accessTierInferred: false, contentType: contentType,
                contentHash: contentHash)));
    }

    public override BlobClient GetBlobClient(string blobName)
    {
        return new FakeBlobClient(blobName, this);
    }

    public override AsyncPageable<BlobItem> GetBlobsAsync(BlobTraits traits = BlobTraits.None,
        BlobStates states = BlobStates.None, string? prefix = null, CancellationToken cancellationToken = default)
    {
        if (ThrowOnList)
        {
            throw new RequestFailedException("Listing failed");
        }
        ListedPrefix = prefix;
        return new FakePageable(ExistingBlobs.FindAll(b => prefix == null || b.Name.StartsWith(prefix)));
    }

    private class FakeBlobClient : BlobClient
    {
        private readonly string _name;
        private readonly FakeBlobContainerClient _container;

        public FakeBlobClient(string name, FakeBlobContainerClient container)
        {
            _name = name;
            _container = container;
        }

        public override string Name => _name;

        public override Task<Response<BlobContentInfo>> UploadAsync(Stream content, BlobUploadOptions options,
            CancellationToken cancellationToken = default)
        {
            _container.Uploads[_name] = options;
            return Task.FromResult<Response<BlobContentInfo>>(null!);
        }
    }

    private class FakePageable : AsyncPageable<BlobItem>
    {
        private readonly List<BlobItem> _items;

        public FakePageable(List<BlobItem> items)
        {
            _items = items;
        }

        public override async IAsyncEnumerator<BlobItem> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        {
            foreach (var item in _items)
            {
                yield return item;
            }
            await Task.CompletedTask;
        }

        public override IAsyncEnumerable<Page<BlobItem>> AsPages(string? continuationToken = null, int? pageSizeHint = null)
        {
            throw new NotImplementedException();
        }
    }
}
