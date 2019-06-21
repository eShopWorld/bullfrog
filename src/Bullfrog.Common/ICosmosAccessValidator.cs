using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Bullfrog.Common
{
    public interface ICosmosAccessValidator<T>
    {
        Task<ValidationResult> ConfirmAccess(T connection);
    }
}
