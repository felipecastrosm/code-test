using System.Collections.Generic;
using System.Threading.Tasks;
using RingbaLibs.Models;

namespace RingbaLibs
{

    public interface IMessageProcessService
    {
        /// <summary>
        /// Process the unit of work and returns if successfull or not
        /// </summary>
        /// <param name="uow"></param>
        /// <returns></returns>
        Task<ActionResult> ProccessMessageAsync(RingbaUOW uow);
    }
}
