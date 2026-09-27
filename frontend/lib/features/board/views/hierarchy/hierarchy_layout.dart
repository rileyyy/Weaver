import 'dart:math' as math;

/// Fixed geometry shared by the Hierarchy header and rows, so both line up.
const double caretColumnWidth = 24;
const double caretGap = 8;
const double hierarchyRowHorizontalPadding = 12;
const double hierarchyResizeHandleWidth = 8;
const double hierarchyIndentPerLevel = 24;

/// How far a row at [depth] indents its caret and title inside a Title
/// column [titleWidth] wide. Capped so the caret always stays inside the
/// column: a deep item squeezes its own title instead of pushing into the
/// Status column and breaking the alignment with the headers.
double hierarchyTitleIndent(int depth, double titleWidth) => math.min(
  depth * hierarchyIndentPerLevel,
  math.max(0, titleWidth - caretColumnWidth - caretGap),
);
