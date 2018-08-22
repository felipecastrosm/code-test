# Ringba Simple Code Test

## What the project does

The main task of the project is to pull a batch of messages off of a messaging queue of some kind, and process each of the messages in the batch. This service will be deployed to a docker cluster and will be auto scaled based on message throughput. The project may have up to N instances running at any time. 

The goal of this project is to utilize good design practices to process the messages as quickly and efficiently as possible. It can be assumed the majority of the work that is done by each message is IO bound work.


## What to do

Utilizing the 4 existing interfaces, you should implement the `ImplementMeService` to process batches of messages in an efficient manner. 

You will use the `IMessageQueService` to retrieve messages to process. To process these messages, you will send the `RingbaUOW` of the message body to the `IMessageProcessService`. If the message is successfully processed, you will update the message with the `MessageCompleted` flag set to true. If the message processing fails, you must set this flag to false which will place the message back in the queue to be tried again. However, every `RingbaUOW` has a `MaxNumberOfRetries` and `MaxAgeInSeconds` that dictates how long the message can be retried for and how many times. `-1` values for these fields should be treated as no limit. If the limit is reached, we should update the message with `MessageCompleted` set to true to remove it from the queue.

Exactly one instance of a given RingbaUOW should be processing at any given time. In other words, there should never be two processes working on the same RingbaUOW concurrently. 

The `Id` of every `RingbaUOW` is unique.


## Notes

Ringba takes logging very seriously and the interface `ILogService` should be used to log as much as possible.

The `IMessageQueService` has some limitations that must be accounted for:
* When messages are "in flight", under heavy load, there is no guarantee that a duplicate message may be delivered with parallel calls to `GetMessagesFromQueAsync`
* Once a message is marked as completed, it is guaranteed not to show back up after 2 seconds

The `IKVRespository` is a Key Value repository that allows atomic fast access to keys stored in a shared out of process repository.

The `ILogService` is a very optimized logging platform that does not block to ship and can be called at will with little to no effect to the performance of the app.

The code in the `ImplementMeService.DoWork` is not to be used but only there as an example of how the flow would run. 


## What is expected

An efficient implementation complete with unit tests must be submitted in order for your response be considered.

This project **IS NOT** designed to run, instead it will be evaluated by executing `dotnet test code-test.test\code-test.test.csproj`.


## How to Submit

Clone the repository and reply back with a zip archive of your completed test with your full name, email, and number you can be reached at. 

Please include your favorite color, favorite book, salary, and any work requirements you may have. 






