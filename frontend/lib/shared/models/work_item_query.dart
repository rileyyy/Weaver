import 'package:weaver/shared/models/tag_match.dart';

/// The inputs of the server's work-item query (ADR-0006) that the board's
/// header drives. The server does the matching; this only carries what to
/// ask for.
///
/// Two queries are equal when the server would answer them the same, so a
/// caller can tell whether a change needs a new request.
class WorkItemQuery {
  WorkItemQuery({
    required this.spaceId,
    String text = '',
    this.tags = const {},
    TagMatch tagMatch = TagMatch.any,
    this.from,
    this.to,
  }) : text = text.trim(),
       // With fewer than two tags, Any and All return the same items.
       tagMatch = tags.length > 1 ? tagMatch : TagMatch.any;

  final String spaceId;

  /// Trimmed; empty means no text filter.
  final String text;

  /// Empty means no tag filter.
  final Set<String> tags;

  /// Always [TagMatch.any] unless there are two or more [tags].
  final TagMatch tagMatch;

  /// Calendar days, inclusive; either may be null (open on that side).
  final DateTime? from;
  final DateTime? to;

  /// True when the query matches every item of the Space, so there's no
  /// need to ask the server which ones match.
  bool get isUnfiltered =>
      text.isEmpty && tags.isEmpty && from == null && to == null;

  @override
  bool operator ==(Object other) =>
      other is WorkItemQuery &&
      other.spaceId == spaceId &&
      other.text == text &&
      other.tags.length == tags.length &&
      other.tags.containsAll(tags) &&
      other.tagMatch == tagMatch &&
      other.from == from &&
      other.to == to;

  @override
  int get hashCode => Object.hash(
    spaceId,
    text,
    Object.hashAllUnordered(tags),
    tagMatch,
    from,
    to,
  );
}
