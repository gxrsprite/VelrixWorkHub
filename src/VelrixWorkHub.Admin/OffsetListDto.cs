using System.Collections.Generic;

public record OffsetListDto<T>(long? Offset, IEnumerable<T> List);
