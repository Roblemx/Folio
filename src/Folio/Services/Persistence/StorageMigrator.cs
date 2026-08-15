namespace Folio.Services.Persistence;

/// <summary>
/// Runs sequential schema migrations on a loaded <see cref="StoredState"/> until it reaches
/// <see cref="CurrentVersion"/>. Only v1 exists today; future versions add cases here.
/// </summary>
public static class StorageMigrator
{
    public const int CurrentVersion = 1;

    public static StoredState Migrate(StoredState state)
    {
        while (state.SchemaVersion < CurrentVersion)
        {
            switch (state.SchemaVersion)
            {
                // Example for the future:
                // case 1: MigrateV1ToV2(state); state.SchemaVersion = 2; break;
                default:
                    state.SchemaVersion = CurrentVersion;
                    break;
            }
        }

        return state;
    }
}
