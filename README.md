# ScriptureRenderingPipeline
A rendering pipeline for scripture and BTTWriter catalog

## Overview
This application is a pipeline that accepts a webhook and then
 processes the repo into a web page. 
 
 Over time more things have been added on such as webhook handler for
 catalog handling for BTTWriter, verse counting for upload
 statistics, and other things

 ### The Bus
 At the core of the application is the bus, which is an Azure Serivce bus
 that is responsible for passing events over to various event consumers
Where everthing starts is at the webhook which will recieve a webhook from WACS validate it, and then put it on the bus.

There are several different topics the bus handles, everything either pushes messages onto the bus or subscribes to new messages for a given topic
Here are the list of topics
- WACSEvent: messages appear here when a webhook is recieved most things listen to this
- RepoRendered: messages appear here when a repo has been rendered
- VerseCountingResult: messages appear here when a repo has had its verses counted

### Rendering
When a push or create WACS event makes it on to the bus this process will download the repo, figure out what type of content it is, 
select a renderer for it and then render and upload the files to Azure Storage.
After that is done it will send a message to the RepoRendered topic with details about what was rendered

### Counting
When a push or create WACS event makes it on to the bus this process will download the repo, figure out what it is and if it is scriptuere
will count the number of chapters and verses in each chapter.
After that it will publish a result to the VerseCountingResult topic with details about the counting.

### The Catalog
The catalog doesn't currently listen to the bus but deals with converstion process directly in the webhook.
After it completes it will insert a record into a cosmos db table which will in turn trigger a rebuild of any catalogs which then get written to azure storage.

## Projects in more detail

### ScriptureRenderingPipeline
This is where the main webhook resides. It used to be where everything happened but a worker process could run the Azure functions worker out of memory so now it is seperate.

### ScriptureRenderingPipelineWorker
This is where the main chunk of the work happens, it listens for bus messages and then renders it and then pushes everything to Azure storage.
Counting also happens here and results are also pushed on to the bus.

### BTTWriterCatalog
This is what generates the catalog for BTTWriter. The main thing that it does is wait for an orgainzation webhook to be recieved and then it will download the repo and then convert it,  and then push it to Azure storage.
After that it will push a record to a cosmos db table which will trigger a rebuild of the catalog. The catalog is then written to Azure storage. The catalog can be manually triggered as well.

### VerseReportingProcessor
This is a simple console app that listens to the VerseCountingResult topic, calculates what the totals should be, and then inserts data into a database as well as sends that information over to PORT.

### PipelineCommon
This is a shared library that is used by all the projects. It contains shared helpers, models, and utilities that are used by all the projects.

### SRPTests
This is where all the tests are located. It is a nunit project that tests projects, helpers, and other things.