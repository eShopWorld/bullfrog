using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Bullfrog.Common
{
    /// <summary>
    /// Performs validation of Cosmos DB connection details.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface ICosmosAccessValidator<T>
    {
        /// <summary>
        /// Checks whether the connection details are correct and at least the read access is granted.
        /// </summary>
        /// <param name="connection">The connection details.</param>
        /// <returns>Return the validation result.</returns>
        Task<ValidationResult> ConfirmAccess(T connection);
    }
}
