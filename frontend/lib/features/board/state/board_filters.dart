import 'package:weaver/core/dates/calendar_days.dart';
import 'package:weaver/core/dates/calendar_window.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/shared/models/tag_match.dart';
import 'package:weaver/shared/models/work_item_query.dart';

/// Everything the board's header controls: time range, search, hidden
/// status columns, selected tags, sort and whether old Done items show.
/// Immutable; each change returns a new value.
///
/// Which items match the time range, search, tags and done-hiding is the
/// server's
/// decision (ADR-0006): [toQuery] says what to ask for. Hidden statuses and
/// sort only change how the result is laid out, so they stay here.
class BoardFilters {
  const BoardFilters({
    this.start,
    this.end,
    this.searchQuery = '',
    this.hiddenStatusIds = const {},
    this.selectedTags = const {},
    this.tagMatch = TagMatch.any,
    this.sortOption = CardSortOption.manual,
    this.showDone = false,
  });

  final DateTime? start;
  final DateTime? end;
  final String searchQuery;
  final Set<String> hiddenStatusIds;

  /// Lower-cased, so "Urgent" and "urgent" are one selection, the same way
  /// the tag chip list merges them.
  final Set<String> selectedTags;
  final TagMatch tagMatch;
  final CardSortOption sortOption;

  /// When false (the default) the server leaves out Done items completed
  /// longer ago than the Space's done-hiding period.
  final bool showDone;

  /// Either bound may be null (open on that side). An inverted range is
  /// swapped rather than silently matching nothing; the pickers prevent
  /// one anyway.
  BoardFilters withTimeRange({DateTime? start, DateTime? end}) {
    final inverted = start != null && end != null && start.isAfter(end);
    return _copy(start: inverted ? end : start, end: inverted ? start : end);
  }

  BoardFilters withSearchQuery(String query) => _copy(searchQuery: query);

  BoardFilters withStatusToggled(String statusId) =>
      _copy(hiddenStatusIds: _toggled(hiddenStatusIds, statusId));

  BoardFilters withTagToggled(String tag) =>
      _copy(selectedTags: _toggled(selectedTags, tag.toLowerCase()));

  BoardFilters withTagMatch(TagMatch match) => _copy(tagMatch: match);

  BoardFilters withSortOption(CardSortOption option) =>
      _copy(sortOption: option);

  BoardFilters withShowDone(bool showDone) => _copy(showDone: showDone);

  bool isTagSelected(String tag) => selectedTags.contains(tag.toLowerCase());

  /// The server query for [spaceId] with this header's time range, search,
  /// tags and done-hiding. Hiding a status or changing the sort leaves it equal, as
  /// neither needs a new request.
  WorkItemQuery toQuery(String spaceId) =>
      _query(spaceId, from: start, to: end);

  /// [toQuery] for the Roadmap showing [window]. The Roadmap is
  /// time-framed, so it always asks for a date window (ADR-0003): the days
  /// both [window] and the header's range cover. Null when they share no
  /// day, so nothing can match.
  WorkItemQuery? toRoadmapQuery(String spaceId, CalendarWindow window) {
    final from = _later(window.start, start);
    final to = _earlier(window.end, end);
    if (daysBetween(from, to) < 0) return null;
    return _query(spaceId, from: from, to: to);
  }

  WorkItemQuery _query(String spaceId, {DateTime? from, DateTime? to}) =>
      WorkItemQuery(
        spaceId: spaceId,
        text: searchQuery,
        tags: selectedTags,
        tagMatch: tagMatch,
        from: from,
        to: to,
        applyDoneHiding: !showDone,
      );

  static DateTime _later(DateTime day, DateTime? other) =>
      other != null && daysBetween(day, other) > 0 ? dateOnly(other) : day;

  static DateTime _earlier(DateTime day, DateTime? other) =>
      other != null && daysBetween(day, other) < 0 ? dateOnly(other) : day;

  static Set<String> _toggled(Set<String> set, String value) =>
      Set.unmodifiable(
        set.contains(value) ? ({...set}..remove(value)) : {...set, value},
      );

  BoardFilters _copy({
    Object? start = _unchanged,
    Object? end = _unchanged,
    String? searchQuery,
    Set<String>? hiddenStatusIds,
    Set<String>? selectedTags,
    TagMatch? tagMatch,
    CardSortOption? sortOption,
    bool? showDone,
  }) => BoardFilters(
    start: identical(start, _unchanged) ? this.start : start as DateTime?,
    end: identical(end, _unchanged) ? this.end : end as DateTime?,
    searchQuery: searchQuery ?? this.searchQuery,
    hiddenStatusIds: hiddenStatusIds ?? this.hiddenStatusIds,
    selectedTags: selectedTags ?? this.selectedTags,
    tagMatch: tagMatch ?? this.tagMatch,
    sortOption: sortOption ?? this.sortOption,
    showDone: showDone ?? this.showDone,
  );

  static const Object _unchanged = Object();
}
