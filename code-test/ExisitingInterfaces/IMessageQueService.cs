using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using RingbaLibs.Models;

namespace RingbaLibs
{

    public interface IMessageQueService: IDisposable
    {
        /// <summary>
        /// Retreives the messages from the queue
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="maxNumberOfMessages">The max number of messages</param>
        /// <param name="emptyQueWaitTimeInSeconds">How long to wait if the queue is empty</param>
        /// <param name="maxMessageProcessTimeInSeconds">How long to hold message out of queue until it is put back on the queue</param>
        /// <returns>up to maxNumberOfMessages</returns>
        Task<MessageBatchResult<T>> GetMessagesFromQueAsync<T>(int maxNumberOfMessages, int emptyQueWaitTimeInSeconds = 30, int maxMessageProcessTimeInSeconds = 30);

        /// <summary>
        /// Updates the message, it either permanently removes message from queue or places the message back on the queue depending
        /// on the value of the MessageCompleted flag
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        Task<ActionResult> UpdateMessagesAsync(IEnumerable<UpdateBatchRequest> request);
    }

    public class UpdateBatchRequest
    {
        public string Id { get; set; }

        public bool MessageCompleted { get; set; }
    }


    public class MessageBatchResult<T> : ActionResult
    {
        public int NumberOfMessages { get; set; }
        public IEnumerable<MessageWrapper<T>> Messages { get; set; }

    }

    public class MessageWrapper<T>
    {
        public T Body { get; set; }

        public string Id { get; set; }
    }
}

