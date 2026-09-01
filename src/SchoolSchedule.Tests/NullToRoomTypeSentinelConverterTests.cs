using System.Globalization;
using SchoolSchedule.App.Helpers;
using SchoolSchedule.Core.Models;

namespace SchoolSchedule.Tests;

public class NullToRoomTypeSentinelConverterTests
{
    private readonly NullToRoomTypeSentinelConverter _converter = new();

    [Fact]
    public void Null_converts_to_sentinel_for_display_in_combobox()
    {
        var result = _converter.Convert(null, typeof(RoomType), null!, CultureInfo.InvariantCulture);

        Assert.Same(NullToRoomTypeSentinelConverter.Sentinel, result);
    }

    [Fact]
    public void Real_room_type_passes_through_convert_unchanged()
    {
        var roomType = new RoomType { Id = 5, Name = "Спортзал" };

        var result = _converter.Convert(roomType, typeof(RoomType), null!, CultureInfo.InvariantCulture);

        Assert.Same(roomType, result);
    }

    [Fact]
    public void Selecting_sentinel_converts_back_to_null()
    {
        var result = _converter.ConvertBack(NullToRoomTypeSentinelConverter.Sentinel, typeof(RoomType), null!, CultureInfo.InvariantCulture);

        Assert.Null(result);
    }

    [Fact]
    public void Selecting_real_room_type_converts_back_unchanged()
    {
        var roomType = new RoomType { Id = 5, Name = "Спортзал" };

        var result = _converter.ConvertBack(roomType, typeof(RoomType), null!, CultureInfo.InvariantCulture);

        Assert.Same(roomType, result);
    }
}
