using Dashboard.React;
using static Dashboard.React.Elements;

namespace Dashboard.Components
{
    /// <summary>
    /// SVG icon components.
    /// </summary>
    public static class Icons
    {
        /// <summary>
        /// Home icon.
        /// </summary>
        public static ReactElement Home() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(
                    d: "M3 9l9-7 9 7v11a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z M9 22V12h6v10",
                    stroke: "currentColor",
                    strokeWidth: 2
                )
            );

        /// <summary>
        /// Users icon.
        /// </summary>
        public static ReactElement Users() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(
                    d: "M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2 M9 7a4 4 0 1 0 0-8 4 4 0 0 0 0 8z M23 21v-2a4 4 0 0 0-3-3.87 M16 3.13a4 4 0 0 1 0 7.75",
                    stroke: "currentColor",
                    strokeWidth: 2
                )
            );

        /// <summary>
        /// User/Doctor icon.
        /// </summary>
        public static ReactElement UserDoctor() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(
                    d: "M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2 M12 7a4 4 0 1 0 0-8 4 4 0 0 0 0 8z M12 14v7 M9 18h6",
                    stroke: "currentColor",
                    strokeWidth: 2
                )
            );

        /// <summary>
        /// Calendar icon.
        /// </summary>
        public static ReactElement Calendar() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(
                    d: "M19 4H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6a2 2 0 0 0-2-2z M16 2v4 M8 2v4 M3 10h18",
                    stroke: "currentColor",
                    strokeWidth: 2
                )
            );

        /// <summary>
        /// Clipboard/Encounter icon.
        /// </summary>
        public static ReactElement Clipboard() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(
                    d: "M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2 M9 2h6a1 1 0 0 1 1 1v2a1 1 0 0 1-1 1H9a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1z",
                    stroke: "currentColor",
                    strokeWidth: 2
                )
            );

        /// <summary>
        /// Heart/Condition icon.
        /// </summary>
        public static ReactElement Heart() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(
                    d: "M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z",
                    stroke: "currentColor",
                    strokeWidth: 2
                )
            );

        /// <summary>
        /// Pill/Medication icon.
        /// </summary>
        public static ReactElement Pill() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(
                    d: "M10.5 20.5L3.5 13.5a4.95 4.95 0 1 1 7-7l7 7a4.95 4.95 0 1 1-7 7z M8.5 8.5l7 7",
                    stroke: "currentColor",
                    strokeWidth: 2
                )
            );

        /// <summary>
        /// Activity/Heartbeat icon.
        /// </summary>
        public static ReactElement Activity() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(
                    d: "M22 12h-4l-3 9L9 3l-3 9H2",
                    stroke: "currentColor",
                    strokeWidth: 2
                )
            );

        /// <summary>
        /// Search icon.
        /// </summary>
        public static ReactElement Search() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(
                    d: "M11 19a8 8 0 1 0 0-16 8 8 0 0 0 0 16z M21 21l-4.35-4.35",
                    stroke: "currentColor",
                    strokeWidth: 2
                )
            );

        /// <summary>
        /// Bell/Notification icon.
        /// </summary>
        public static ReactElement Bell() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(
                    d: "M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9 M13.73 21a2 2 0 0 1-3.46 0",
                    stroke: "currentColor",
                    strokeWidth: 2
                )
            );

        /// <summary>
        /// Settings icon.
        /// </summary>
        public static ReactElement Settings() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(
                    d: "M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z",
                    stroke: "currentColor",
                    strokeWidth: 2
                )
            );

        /// <summary>
        /// ChevronLeft icon.
        /// </summary>
        public static ReactElement ChevronLeft() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(d: "M15 18l-6-6 6-6", stroke: "currentColor", strokeWidth: 2)
            );

        /// <summary>
        /// ChevronRight icon.
        /// </summary>
        public static ReactElement ChevronRight() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(d: "M9 18l6-6-6-6", stroke: "currentColor", strokeWidth: 2)
            );

        /// <summary>
        /// Plus icon.
        /// </summary>
        public static ReactElement Plus() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(d: "M12 5v14 M5 12h14", stroke: "currentColor", strokeWidth: 2)
            );

        /// <summary>
        /// Edit/Pencil icon.
        /// </summary>
        public static ReactElement Edit() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(
                    d: "M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7 M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4 9.5-9.5z",
                    stroke: "currentColor",
                    strokeWidth: 2
                )
            );

        /// <summary>
        /// Trash icon.
        /// </summary>
        public static ReactElement Trash() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(
                    d: "M3 6h18 M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2",
                    stroke: "currentColor",
                    strokeWidth: 2
                )
            );

        /// <summary>
        /// Eye icon.
        /// </summary>
        public static ReactElement Eye() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(
                    d: "M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z",
                    stroke: "currentColor",
                    strokeWidth: 2
                )
            );

        /// <summary>
        /// Refresh icon.
        /// </summary>
        public static ReactElement Refresh() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(
                    d: "M23 4v6h-6 M1 20v-6h6 M3.51 9a9 9 0 0 1 14.85-3.36L23 10 M1 14l4.64 4.36A9 9 0 0 0 20.49 15",
                    stroke: "currentColor",
                    strokeWidth: 2
                )
            );

        /// <summary>
        /// TrendUp icon.
        /// </summary>
        public static ReactElement TrendUp() =>
            Svg(
                className: "icon",
                width: 16,
                height: 16,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(d: "M23 6l-9.5 9.5-5-5L1 18", stroke: "currentColor", strokeWidth: 2)
            );

        /// <summary>
        /// TrendDown icon.
        /// </summary>
        public static ReactElement TrendDown() =>
            Svg(
                className: "icon",
                width: 16,
                height: 16,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(d: "M23 18l-9.5-9.5-5 5L1 6", stroke: "currentColor", strokeWidth: 2)
            );

        /// <summary>
        /// Menu icon (hamburger).
        /// </summary>
        public static ReactElement Menu() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(d: "M3 12h18M3 6h18M3 18h18", stroke: "currentColor", strokeWidth: 2)
            );

        /// <summary>
        /// X/Close icon.
        /// </summary>
        public static ReactElement X() =>
            Svg(
                className: "icon",
                width: 20,
                height: 20,
                viewBox: "0 0 24 24",
                fill: "none",
                children: Path(d: "M18 6L6 18M6 6l12 12", stroke: "currentColor", strokeWidth: 2)
            );
    }
}
