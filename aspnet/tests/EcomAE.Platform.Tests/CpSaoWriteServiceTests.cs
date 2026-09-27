using System.Collections.Generic;
using EcomAE.Platform.Cp;
using Xunit;

namespace EcomAE.Platform.Tests;

public sealed class CpSaoWriteServiceTests
{
    [Fact]
    public void State_fields_follow_the_php_select_item_status_naming()
    {
        var map = CpSaoWriteService.ParseStateFields(
        [
            new("confirmWrites", "true"),
            new("save_action", "save_action"),
            new("select_item_status_3", "12"),
            new("select_item_status_4", "0"),
        ]);

        Assert.Equal(2, map.Count);
        Assert.Equal(12, map[3]);
        Assert.Equal(0, map[4]);
    }

    [Fact]
    public void Non_numeric_and_negative_fields_are_rejected_or_clamped()
    {
        var map = CpSaoWriteService.ParseStateFields(
        [
            new("select_item_status_abc", "3"),
            new("select_item_status_0", "3"),
            new("select_item_status_-1", "3"),
            new("select_item_status_9", "-4"),
            new("select_item_status_10", "not a number"),
        ]);

        Assert.Equal(new Dictionary<long, long> { [9] = 0, [10] = 0 }, map);
    }
}
