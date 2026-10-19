import 'package:flutter/material.dart';

/// Grid lines for the swim-lane board's table/grid styling — a fixed, mid
/// contrast white rather than a theme-derived color, since the board area
/// behind the grid is always dark regardless of light/dark theme.
const Color gridLineColor = Colors.white24;
const Color onGridBackground = Colors.white;

const double laneLabelWidth = 160;
const double minColumnWidth = 240;
const double headerRowHeight = 48;

/// A collapsed swimlane's row height — just enough for its label's single
/// line and the collapse toggle, matching the status header row's height.
const double collapsedSwimlaneRowHeight = headerRowHeight;

/// How much larger the status column headers and swimlane ("project")
/// labels render than the theme's base title styles — the swim-lane grid's
/// row/column headers, so they read clearly against the grid's darker
/// background.
const double gridHeaderFontScale = 1.2;

/// Space around every card in a status column's two-per-row grid: between
/// cards in a row, between rows, and between the cards and the cell's edge.
const double cardGridSpacing = 12;

/// The gap between a status column cell and the grid lines around it.
const double statusColumnMargin = 4;

/// A status column cell's margin plus its [cardGridSpacing] padding, on both
/// sides, subtracted from its outer width/height to get the space actually
/// left for cards.
const double statusColumnChrome = 2 * (statusColumnMargin + cardGridSpacing);

/// Horizontal space [SwimlaneLabel] gives to its collapse chevron and
/// padding before its title even starts wrapping: left+right padding
/// (4+8), the chevron's own width (24), and the gap after it (4).
const double swimlaneLabelChrome = 40;
