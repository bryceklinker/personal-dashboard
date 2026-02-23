namespace Personal.Dashboard.Models;

public record ListResultModel<T>(T[] Items, long Total);

public record PagedListResultModel<T>(T[] Items, long Total, long Offset, long Limit)
    : ListResultModel<T>(Items, Total);