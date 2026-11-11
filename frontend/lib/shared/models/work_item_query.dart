import 'package:weaver/shared/models/tag_match.dart';

/// The inputs of the server's work-item query (ADR-0006) that the board's
/// header drives. The server does the matching; this only carries what to
/// ask for.
///
/// Two queries are equal when the server would answer them the same, so a
/// caller can tell whether a change needs a new request.
class WorkItemQuery {
  WorkItemQuery({
    required this.spaceIds,
    String text = '',
    this.tags = const {},
    TagMatch tagMatch = TagMatch.any,
    this.from,
    this.to,
    this.applyDoneHiding = false,
    this.backlogOnly = false,
    this.scheduledOrInProgressOnly = false,
  }) : text = text.trim(),
       // With fewer than two tags, Any and All return the same items.
       tagMatch = tags.length > 1 ? tagMatch : TagMatch.any;

  /// The Spaces to search; empty for every Space.
  final Set<String> spaceIds;

  /// Trimmed; empty means no text filter.
  final String text;

  /// Empty means no tag filter.
  final Set<String> tags;

  /// Always [TagMatch.any] unless there are two or more [tags].
  final TagMatch tagMatch;

  /// Calendar days, inclusive; either may be null (open on that side).
  final DateTime? from;
  final DateTime? to;

  /// Leaves out Done items completed longer ago than their Space's
  /// done-hiding period. The server works out the cutoff from its own
  /// calendar, so nothing date-like is sent for it.
  final bool applyDoneHiding;

  /// Only the Backlog: items that aren't Done and have no dates (ADR-0003).
  final bool backlogOnly;

  /// Overview's rule: only items in a Doing status or with dates in the
  /// window (any dated item with no window), plus their undated ancestors.
  final bool scheduledOrInProgressOnly;

  /// True when the query matches every item of its Spaces, so there's no
  /// need to ask the server which ones match.
  bool get isUnfiltered =>
      text.isEmpty &&
      tags.isEmpty &&
      from == null &&
      to == null &&
      !applyDoneHiding &&
      !backlogOnly &&
      !scheduledOrInProgressOnly;

  @override
  bool operator ==(Object other) =>
      other is WorkItemQuery &&
      other.spaceIds.length == spaceIds.length &&
      other.spaceIds.containsAll(spaceIds) &&
      other.text == text &&
      other.tags.length == tags.length &&
      other.tags.containsAll(tags) &&
      other.tagMatch == tagMatch &&
      other.from == from &&
      other.to == to &&
      other.applyDoneHiding == applyDoneHiding &&
      other.backlogOnly == backlogOnly &&
      other.scheduledOrInProgressOnly == scheduledOrInProgressOnly;

  @override
  int get hashCode => Object.hash(
    Object.hashAllUnordered(spaceIds),
    text,
    Object.hashAllUnordered(tags),
    tagMatch,
    from,
    to,
    applyDoneHiding,
    backlogOnly,
    scheduledOrInProgressOnly,
  );
}
