using System;

namespace Audit.EntityFramework.ConfigurationApi
{
    /// <summary>
    /// Configurates the OptIn mode
    /// </summary>
    /// <typeparam name="T">The AuditDbContext type</typeparam>
    public interface IIncludeConfigurator<T>
        where T : AuditDbContext
    {
        /// <summary>
        /// Includes the given entity type.
        /// </summary>
        /// <typeparam name="TEntity">The type of the EF entity to include.</typeparam>
        IncludeConfigurator<T> Include<TEntity>();
        /// <summary>
        /// Includes the given entity type.
        /// </summary>
        /// <param name="entityType">The entity type to include.</param>
        IncludeConfigurator<T> Include(Type entityType);
    }
}