import 'package:weaver/shared/models/tag_match.dart';

/// The inputs of the server's work-item query (ADR-0006) that the board's
/// header drives. The server does the matching; this only carries what to
/// ask for.
class WorkItemQuery {
  const WorkItemQuery({
    required this.spaceId,
    this.text = '',
    this.tags = const {},
    this.tagMatch = TagMatch.any,
    this.from,
    this.to,
  });

  final String spaceId;

  /// Blank means no text filter.
  final String text;

  /// Empty means no tag filter.
  final Set<String> tags;
  final TagMatch tagMatch;

  /// Calendar days, inclusive; either may be null (open on that side).
  final DateTime? from;
  final DateTime? to;

  /// True when the query matches every item of the Space, so there's no
  /// need to ask the server which ones match.
  bool get isUnfiltered =>
      text.trim().isEmpty && tags.isEmpty && from == null && to == null;
}
