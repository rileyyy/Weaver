import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/views/swimlane/widgets/grid_row_box.dart';
import 'package:weaver/features/board/views/swimlane/widgets/status_column.dart';
import 'package:weaver/features/board/views/swimlane/widgets/swimlane_label.dart';
import 'package:weaver/features/board/widgets/assignee_avatar.dart';

/// The swimlane board itself: a pinned lane-label column on the left plus
/// a columns area on the right. When [width] is wide enough to give every
/// status column at least [minColumnWidth] alongside the label column,
/// the columns stretch evenly to fill the remaining width and nothing
/// scrolls horizontally. Otherwise each column keeps [minColumnWidth] and
/// only the columns area scrolls horizontally — the label column, being a
/// sibling outside that scroll view, stays in view.
class SwimlaneView extends StatelessWidget {
  /// Grid lines for the swim-lane board's table/grid styling — a fixed, mid
  /// contrast white rather than a theme-derived color, since the board's
  /// background ([_BoardViewState._buildBoardArea]) is always dark regardless
  /// of light/dark theme.
  static const Color gridLineColor = Colors.white24;
  static const Color onGridBackground = Colors.white;

  static const double laneLabelWidth = 160;
  static const double minColumnWidth = 240;
  static const double headerRowHeight = 48;

  /// A collapsed swimlane's row height — just enough for its label's single
  /// line and the collapse toggle, matching the status header row's height.
  static const double collapsedSwimlaneRowHeight = headerRowHeight;

  /// How much larger the status column headers and swimlane ("project")
  /// labels render than the theme's base title styles — the swim-lane grid's
  /// row/column headers, so they read clearly against the grid's darker
  /// background.
  static const double gridHeaderFontScale = 1.2;

  /// Gap between cards in a status column's two-per-row grid, both between
  /// columns and between rows of cards.
  static const double cardGridSpacing = 8;

  /// A status column cell's own margin + padding (each `EdgeInsets.all(4)`),
  /// subtracted from its outer width/height to get the space actually left
  /// for cards.
  static const double statusColumnChrome = 16;

  /// Horizontal space [_SwimlaneLabel] gives to its collapse chevron and
  /// padding before its title even starts wrapping: left+right padding
  /// (4+8), the chevron's own width (24), and the gap after it (4).
  static const double swimlaneLabelChrome = 40;

  const SwimlaneView({
    super.key,
    required this.width,
    required this.statuses,
    required this.swimlanes,
    required this.onCardDropped,
    required this.onCardReparented,
    required this.onCardDetailsOpened,
    required this.onSwimlaneLabelTapped,
    required this.onAssignRequested,
    required this.cardVisible,
    required this.cardComparator,
    required this.assigneeInitialFor,
    required this.collapsedSwimlaneIds,
    required this.onToggleSwimlaneCollapsed,
    required this.onToggleAllSwimlanesCollapsed,
  });

  final double width;
  final List<BoardStatus> statuses;
  final List<Swimlane> swimlanes;
  final Future<void> Function(WorkItemCard card, String newStatusId)
  onCardDropped;
  final Future<void> Function(WorkItemCard card, String newParentId)
  onCardReparented;
  final void Function(WorkItemCard card) onCardDetailsOpened;
  final void Function(String workItemId) onSwimlaneLabelTapped;
  final void Function(String workItemId, String? currentAssigneeId)
  onAssignRequested;
  final bool Function(WorkItemCard card) cardVisible;
  final Comparator<WorkItemCard>? cardComparator;
  final String? Function(String? userId) assigneeInitialFor;
  final Set<String> collapsedSwimlaneIds;
  final void Function(String parentId) onToggleSwimlaneCollapsed;
  final VoidCallback onToggleAllSwimlanesCollapsed;

  @override
  Widget build(BuildContext context) {
    final availableForColumns = width - laneLabelWidth;
    final useFlexColumns =
        statuses.isNotEmpty &&
        availableForColumns >= statuses.length * minColumnWidth;
    final columnOuterWidth = useFlexColumns
        ? availableForColumns / statuses.length
        : minColumnWidth;
    final cardWidth =
        (columnOuterWidth - statusColumnChrome - cardGridSpacing) / 2;
    // Half of cardWidth, not cardWidth itself — cards no longer need to be
    // square; fitting more information per card matters more than the
    // shape, so they're shorter and squatter instead.
    final cardHeight = cardWidth * 0.5;

    final baseHeaderStyle = Theme.of(context).textTheme.titleMedium;
    final headerTextStyle = baseHeaderStyle?.copyWith(
      fontSize: (baseHeaderStyle.fontSize ?? 16) * gridHeaderFontScale,
      color: onGridBackground,
      fontWeight: FontWeight.w600,
    );

    final allSwimlanesCollapsed =
        swimlanes.isNotEmpty &&
        swimlanes.every((lane) => collapsedSwimlaneIds.contains(lane.parentId));

    final labelColumn = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        GridRowBox(
          height: headerRowHeight,
          showBottomBorder: true,
          child: Align(
            alignment: Alignment.centerLeft,
            child: IconButton(
              iconSize: 20,
              visualDensity: VisualDensity.compact,
              color: onGridBackground,
              icon: Icon(
                allSwimlanesCollapsed ? Icons.unfold_more : Icons.unfold_less,
              ),
              tooltip: allSwimlanesCollapsed ? 'Expand all' : 'Collapse all',
              onPressed: onToggleAllSwimlanesCollapsed,
            ),
          ),
        ),
        for (final lane in swimlanes)
          GridRowBox(
            height: _rowHeightFor(context, lane, cardHeight),
            showBottomBorder: true,
            child: SwimlaneLabel(
              swimlane: lane,
              assigneeInitial: assigneeInitialFor(lane.assignedToUserId),
              isCollapsed: collapsedSwimlaneIds.contains(lane.parentId),
              onCardReparented: onCardReparented,
              onTapped: onSwimlaneLabelTapped,
              onAssignTapped: () =>
                  onAssignRequested(lane.parentId, lane.assignedToUserId),
              onToggleCollapsed: () => onToggleSwimlaneCollapsed(lane.parentId),
            ),
          ),
      ],
    );

    final headerRow = GridRowBox(
      height: headerRowHeight,
      showBottomBorder: true,
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (final status in statuses)
            _columnWrapper(
              flex: useFlexColumns,
              showRightBorder: status != statuses.last,
              child: Padding(
                padding: const EdgeInsets.all(8),
                child: Align(
                  alignment: Alignment.centerLeft,
                  child: Text(status.name, style: headerTextStyle),
                ),
              ),
            ),
        ],
      ),
    );

    final laneRows = [
      for (final lane in swimlanes)
        GridRowBox(
          height: _rowHeightFor(context, lane, cardHeight),
          showBottomBorder: true,
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              for (final status in statuses)
                _columnWrapper(
                  flex: useFlexColumns,
                  showRightBorder: status != statuses.last,
                  child: StatusColumn(
                    swimlane: lane,
                    status: status,
                    cardWidth: cardWidth,
                    cardHeight: cardHeight,
                    isCollapsed: collapsedSwimlaneIds.contains(lane.parentId),
                    onCardDropped: onCardDropped,
                    onCardDetailsOpened: onCardDetailsOpened,
                    onAssignRequested: onAssignRequested,
                    cardVisible: cardVisible,
                    cardComparator: cardComparator,
                    assigneeInitialFor: assigneeInitialFor,
                  ),
                ),
            ],
          ),
        ),
    ];

    final columnsContent = Column(children: [headerRow, ...laneRows]);
    final columnsArea = useFlexColumns
        ? columnsContent
        : SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: SizedBox(
              width: statuses.length * minColumnWidth,
              child: columnsContent,
            ),
          );

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(width: laneLabelWidth, child: labelColumn),
        Expanded(child: columnsArea),
      ],
    );
  }

  /// A collapsed lane gets the taller of [collapsedSwimlaneRowHeight] (room
  /// enough for a collapsed status cell's card-count text) or its label's
  /// own wrapped-title height (see [_swimlaneLabelMinHeight]) — a collapsed
  /// label still soft-wraps rather than eliding, so a long title can still
  /// need more than one line even while collapsed.
  ///
  /// An expanded lane gets the taller of: enough rows of [cardHeight] cards
  /// for the busiest status column in this lane (plus always at least one
  /// trailing empty slot, so a full grid never looks completely "closed" —
  /// dropping one more card always has visible room to land in; defaulting
  /// to a single row of two slots when the lane is empty, rather than
  /// reserving a taller 2x2 grid up front), or whatever the lane's own label
  /// needs to fit its (possibly wrapped) title and assignee avatar.
  double _rowHeightFor(BuildContext context, Swimlane lane, double cardHeight) {
    if (collapsedSwimlaneIds.contains(lane.parentId)) {
      final labelHeight = _swimlaneLabelMinHeight(
        context,
        lane,
        showsAvatar: false,
      );
      return labelHeight > collapsedSwimlaneRowHeight
          ? labelHeight
          : collapsedSwimlaneRowHeight;
    }

    var maxCount = 0;
    for (final status in statuses) {
      final count = lane.cards
          .where((c) => c.statusId == status.id && cardVisible(c))
          .length;
      if (count > maxCount) maxCount = count;
    }

    final rows = ((maxCount + 1) / 2).ceil();
    final cardBasedHeight =
        rows * cardHeight + (rows - 1) * cardGridSpacing + statusColumnChrome;
    final labelHeight = _swimlaneLabelMinHeight(
      context,
      lane,
      showsAvatar: true,
    );
    return cardBasedHeight > labelHeight ? cardBasedHeight : labelHeight;
  }

  /// The [_SwimlaneLabel]'s own minimum height: its top/bottom padding, plus
  /// its (possibly multi-line, wrapped) title, plus — when [showsAvatar] —
  /// the fixed gap and assignee avatar below it. Measured directly rather
  /// than guessed, since the label and the status-columns row it sits
  /// beside must always end up exactly the same height (see [GridRowBox]).
  double _swimlaneLabelMinHeight(
    BuildContext context,
    Swimlane lane, {
    required bool showsAvatar,
  }) {
    final baseLabelStyle = Theme.of(context).textTheme.titleSmall;
    final labelStyle = baseLabelStyle?.copyWith(
      fontSize: (baseLabelStyle.fontSize ?? 14) * gridHeaderFontScale,
    );
    final textPainter = TextPainter(
      text: TextSpan(text: lane.title, style: labelStyle),
      textDirection: TextDirection.ltr,
      textScaler: MediaQuery.textScalerOf(context),
    )..layout(maxWidth: laneLabelWidth - swimlaneLabelChrome);

    const topPadding = 16;
    const bottomPadding = 8;
    const gapBeforeAvatar = 24;
    // A small cushion on top of the measured text height — TextPainter's
    // layout outside the widget tree can land a pixel or so short of what
    // the actual Text widget renders (rounding, font metrics), and this is
    // a floor other content must never clip against.
    const measurementSafetyMargin = 4;
    var height =
        topPadding +
        textPainter.height +
        measurementSafetyMargin +
        bottomPadding;
    if (showsAvatar) height += gapBeforeAvatar + AssigneeAvatar.cardSize;
    return height;
  }

  Widget _columnWrapper({
    required bool flex,
    required bool showRightBorder,
    required Widget child,
  }) {
    final content = showRightBorder
        ? DecoratedBox(
            decoration: const BoxDecoration(
              border: Border(right: BorderSide(color: gridLineColor)),
            ),
            child: child,
          )
        : child;
    return flex
        ? Expanded(child: content)
        : SizedBox(width: minColumnWidth, child: content);
  }
}
