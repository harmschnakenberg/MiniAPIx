namespace MiniAPI.Db
{
    public partial class Sql
    {
        internal static async void SaveTags(string v)
        {
            _ = await Sql.NonQueryAsync(DailyDbPath, v, null); ;        
        }

    }
}
