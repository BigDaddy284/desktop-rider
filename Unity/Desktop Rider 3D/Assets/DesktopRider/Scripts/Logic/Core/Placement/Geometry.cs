#nullable enable

using System;
using System.Collections.Generic;

namespace DesktopRider.Core.Placement
{
    /// <summary>A point on the desktop in physical screen pixels. Screens left of or above the main one have negative values.</summary>
    public readonly struct PixelPoint : IEquatable<PixelPoint>
    {
        public PixelPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }

        public bool Equals(PixelPoint other) => X == other.X && Y == other.Y;

        public override bool Equals(object? obj) => obj is PixelPoint other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y);

        public override string ToString() => $"PixelPoint {{ X = {X}, Y = {Y} }}";

        public void Deconstruct(out int x, out int y)
        {
            x = X;
            y = Y;
        }

        public static bool operator ==(PixelPoint left, PixelPoint right) => left.Equals(right);

        public static bool operator !=(PixelPoint left, PixelPoint right) => !left.Equals(right);
    }

    /// <summary>A width and height in physical screen pixels.</summary>
    public readonly struct PixelSize : IEquatable<PixelSize>
    {
        public PixelSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }

        public int Height { get; }

        public bool Equals(PixelSize other) => Width == other.Width && Height == other.Height;

        public override bool Equals(object? obj) => obj is PixelSize other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Width, Height);

        public override string ToString() => $"PixelSize {{ Width = {Width}, Height = {Height} }}";

        public void Deconstruct(out int width, out int height)
        {
            width = Width;
            height = Height;
        }

        public static bool operator ==(PixelSize left, PixelSize right) => left.Equals(right);

        public static bool operator !=(PixelSize left, PixelSize right) => !left.Equals(right);
    }

    /// <summary>A rectangle on the desktop in physical screen pixels.</summary>
    public readonly struct PixelRect : IEquatable<PixelRect>
    {
        public PixelRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int X { get; }

        public int Y { get; }

        public int Width { get; }

        public int Height { get; }

        public int Right => X + Width;

        public int Bottom => Y + Height;

        public PixelPoint Location => new(X, Y);

        public PixelSize Size => new(Width, Height);

        public static PixelRect From(PixelPoint location, PixelSize size) => new(location.X, location.Y, size.Width, size.Height);

        /// <summary>The number of pixels this rectangle shares with another; zero when they do not overlap.</summary>
        public long OverlapArea(PixelRect other)
        {
            long width = Math.Min(Right, other.Right) - Math.Max(X, other.X);
            long height = Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y);
            return width > 0 && height > 0 ? width * height : 0;
        }

        public bool Equals(PixelRect other) => X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

        public override bool Equals(object? obj) => obj is PixelRect other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

        public override string ToString() => $"PixelRect {{ X = {X}, Y = {Y}, Width = {Width}, Height = {Height} }}";

        public void Deconstruct(out int x, out int y, out int width, out int height)
        {
            x = X;
            y = Y;
            width = Width;
            height = Height;
        }

        public static bool operator ==(PixelRect left, PixelRect right) => left.Equals(right);

        public static bool operator !=(PixelRect left, PixelRect right) => !left.Equals(right);
    }

    /// <summary>One monitor. The working area is the part not covered by the taskbar.</summary>
    public sealed record ScreenArea(string DeviceName, PixelRect WorkingArea, bool IsPrimary);

    /// <summary>Supplies the monitors currently attached to the computer.</summary>
    public interface IScreenProvider
    {
        IReadOnlyList<ScreenArea> GetScreens();
    }

    /// <summary>Which side of the character a speech bubble or the conversation window sits on.</summary>
    public enum CompanionSide
    {
        Above,
        Left,
        Right,
        Below,
    }

    /// <summary>Where to put a speech bubble or the conversation window, and which side of the character that is.</summary>
    public readonly struct CompanionPlacement : IEquatable<CompanionPlacement>
    {
        public CompanionPlacement(PixelPoint position, CompanionSide side)
        {
            Position = position;
            Side = side;
        }

        public PixelPoint Position { get; }

        public CompanionSide Side { get; }

        public bool Equals(CompanionPlacement other) => Position == other.Position && Side == other.Side;

        public override bool Equals(object? obj) => obj is CompanionPlacement other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Position, Side);

        public override string ToString() => $"CompanionPlacement {{ Position = {Position}, Side = {Side} }}";

        public void Deconstruct(out PixelPoint position, out CompanionSide side)
        {
            position = Position;
            side = Side;
        }

        public static bool operator ==(CompanionPlacement left, CompanionPlacement right) => left.Equals(right);

        public static bool operator !=(CompanionPlacement left, CompanionPlacement right) => !left.Equals(right);
    }
}
