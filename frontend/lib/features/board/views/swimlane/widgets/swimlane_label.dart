import 'dart:async';

import 'package:flutter/material.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/views/swimlane/swimlane_view.dart';
import 'package:weaver/features/board/widgets/assignee_avatar.dart';

class SwimlaneLabel extends StatelessWidget {
  const SwimlaneLabel({
    super.key,
    required this.swimlane,
    required this.assigneeInitial,
    required this.isCollapsed,
    required this.onCardReparented,
    required this.onTapped,
    required this.onAssignTapped,
    required this.onToggleCollapsed,
  });

  final Swimlane swimlane;

  /// The swimlane's own ("project") work item's assignee, resolved by the
  /// view model — see [BoardCard.assigneeInitial].
  final String? assigneeInitial;

  final bool isCollapsed;

  final Future<void> Function(WorkItemCard card, String newParentId)
  onCardReparented;
  final void Function(String workItemId) onTapped;
  final VoidCallback onAssignTapped;
  final VoidCallback onToggleCollapsed;

  @override
  Widget build(BuildContext context) {
    return DragTarget<WorkItemCard>(
      onWillAcceptWithDetails: (details) =>
          details.data.parentId != swimlane.parentId,
      onAcceptWithDetails: (details) =>
          unawaited(onCardReparented(details.data, swimlane.parentId)),
      builder: (context, candidateData, rejectedData) {
        final colorScheme = Theme.of(context).colorScheme;
        final borderRadius = BorderRadius.circular(8);
        final baseLabelStyle = Theme.of(context).textTheme.titleSmall;
        final labelStyle = baseLabelStyle?.copyWith(
          fontSize: (baseLabelStyle.fontSize ?? 14) * SwimlaneView.gridHeaderFontScale,
          color: SwimlaneView.onGridBackground,
          fontWeight: FontWeight.w600,
          decoration: TextDecoration.underline,
        );
        return Material(
          color: candidateData.isNotEmpty
              ? colorScheme.primaryContainer.withValues(alpha: 0.4)
              : Colors.transparent,
          borderRadius: borderRadius,
          child: InkWell(
            borderRadius: borderRadius,
            onTap: () => onTapped(swimlane.parentId),
            child: Padding(
              // Top-aligned rather than centered in the row (see
              // crossAxisAlignment below), with extra top margin so the
              // title doesn't sit flush against the row's own top edge —
              // and left free to wrap to multiple lines instead of
              // eliding, now that it's not vertically centered to make
              // room for a taller label.
              padding: const EdgeInsets.only(
                left: 4,
                right: 8,
                top: 16,
                bottom: 8,
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Fixed size regardless of collapse state — matches the
                  // Hierarchy view's own expand/collapse caret treatment.
                  SizedBox(
                    width: 24,
                    height: 24,
                    child: IconButton(
                      padding: EdgeInsets.zero,
                      iconSize: 18,
                      constraints: const BoxConstraints(),
                      visualDensity: VisualDensity.compact,
                      color: SwimlaneView.onGridBackground,
                      icon: Icon(
                        isCollapsed ? Icons.chevron_right : Icons.expand_more,
                      ),
                      tooltip: isCollapsed ? 'Expand' : 'Collapse',
                      onPressed: onToggleCollapsed,
                    ),
                  ),
                  const SizedBox(width: 4),
                  Expanded(
                    child: isCollapsed
                        ? Text(
                            swimlane.title,
                            style: labelStyle,
                            softWrap: true,
                          )
                        : Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Text(
                                swimlane.title,
                                style: labelStyle,
                                softWrap: true,
                              ),
                              // ~24px between the title and its own
                              // assignee avatar below it, so they don't
                              // sit too close together.
                              const SizedBox(height: 24),
                              // Same size as a card's own avatar (see
                              // AssigneeAvatar.cardSize) so the two read as
                              // the same visual weight, plus a silhouette
                              // placeholder when unassigned — a lane's own
                              // assignee is prominent enough to always show
                              // a tappable target, unlike a card's (which
                              // stays compact when it has no assignee).
                              AssigneeAvatar(
                                initial: assigneeInitial,
                                size: AssigneeAvatar.cardSize,
                                showPlaceholderWhenUnassigned: true,
                                onTap: onAssignTapped,
                              ),
                            ],
                          ),
                  ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }
}
