using System.Threading.Tasks;

namespace Bee.Hub.EfCore.Stores
{
    public interface IInboxStore
    {
        Task<bool> TryMarkProcessedAsync(string messageId, string handler);
        Task<bool> IsProcessedAsync(string messageId);
    }
}
