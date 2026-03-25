namespace Personal.Dashboard.Core.Common.Exceptions;

public class EntityNotFoundException(Type entityType, object id)
    : Exception($"{entityType.Name} with id '{id}' was not found.");

public class EntityNotFoundException<TEntity>(object id)
    : EntityNotFoundException(typeof(TEntity), id);
