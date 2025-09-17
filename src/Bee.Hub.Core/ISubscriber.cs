using System.Threading.Tasks;

namespace Bee.Hub.Core
{
    public interface ISubscriber<T>
    {
        Task HandleAsync(T message, MessageContext context);
    }
}