namespace RingbaLibs.Models
{
    public class ActionResult
    {
        public string ErrorMessage { get; set; }
        public int ErrorCode { get; set; }

        /// <summary>
        /// Bool value if the action was successfull
        /// </summary>
        public bool IsSuccessfull { get; set; }
    }
}
