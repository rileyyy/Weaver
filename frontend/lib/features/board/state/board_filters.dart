import 'package:weaver/core/dates/calendar_days.dart';
import 'package:weaver/core/dates/calendar_window.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/features/board/models/view_definition.dart';
import 'package:weaver/features/board/models/view_layout.dart';
import 'package:weaver/shared/models/tag_match.dart';
import 'package:weaver/shared/models/work_item_query.dart';

/// Everything the board's header controls: which Spaces, time range,
/// search, hidden status columns, selected tags, sort, whether old Done
/// items show and Overview's rule. Immutable; each change returns a new
/// value. A View stores some of these ([withView], [toDefinition]).
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
    this.spaceIds = const {},
    this.scheduledOrInProgressOnly = false,
  });

  final DateTime? start;
  final DateTime? end;
  final String searchQuery;
  final Set<String> hiddenStatusIds;

  /// As first chosen, so a saved View keeps the tag's spelling. "Urgent"
  /// and "urgent" are one selection, the same way the tag chip list merges
  /// them.
  final Set<String> selectedTags;
  final TagMatch tagMatch;
  final CardSortOption sortOption;

  /// When false (the default) the server leaves out Done items completed
  /// longer ago than the Space's done-hiding period.
  final bool showDone;

  /// The Spaces shown; empty for every Space.
  final Set<String> spaceIds;

  /// Overview's rule: only items in a Doing status or with dates.
  final bool scheduledOrInProgressOnly;

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

  BoardFilters withTagToggled(String tag) => _copy(
    selectedTags: Set.unmodifiable(
      isTagSelected(tag)
          ? {
              for (final selected in selectedTags)
                if (selected.toLowerCase() != tag.toLowerCase()) selected,
            }
          : {...selectedTags, tag},
    ),
  );

  BoardFilters withTagMatch(TagMatch match) => _copy(tagMatch: match);

  BoardFilters withSortOption(CardSortOption option) =>
      _copy(sortOption: option);

  BoardFilters withShowDone(bool showDone) => _copy(showDone: showDone);

  BoardFilters withSpaces(Set<String> spaceIds) =>
      _copy(spaceIds: Set.unmodifiable(spaceIds));

  BoardFilters withScheduledOrInProgressOnly(bool only) =>
      _copy(scheduledOrInProgressOnly: only);

  /// These filters with [view]'s query inputs in place of the header's own.
  /// A View stores no time range, hidden statuses or sort, so those stay.
  BoardFilters withView(ViewDefinition view) => _copy(
    spaceIds: Set.unmodifiable(view.spaceIds),
    searchQuery: view.text,
    selectedTags: Set.unmodifiable(view.tags),
    tagMatch: view.tagMatch,
    showDone: !view.applyDoneHiding,
    scheduledOrInProgressOnly: view.scheduledOrInProgressOnly,
  );

  /// A View named [name] that stores these filters' query inputs.
  ViewDefinition toDefinition(String name, ViewLayout layout) => ViewDefinition(
    name: name,
    spaceIds: spaceIds,
    text: searchQuery.trim(),
    tags: selectedTags,
    tagMatch: tagMatch,
    applyDoneHiding: !showDone,
    scheduledOrInProgressOnly: scheduledOrInProgressOnly,
    layout: layout,
  );

  bool isTagSelected(String tag) =>
      _selectedTagsLowered.contains(tag.toLowerCase());

  /// The server ignores case, so the query asks lower-cased: changing only
  /// a tag's case then asks nothing new.
  Set<String> get _selectedTagsLowered => {
    for (final tag in selectedTags) tag.toLowerCase(),
  };

  /// The server query over [shownSpaceIds] with this header's time range,
  /// search, tags, done-hiding and Overview's rule. Hiding a status or
  /// changing the sort leaves it equal, as neither needs a new request.
  WorkItemQuery toQuery(Set<String> shownSpaceIds) => _query(
    shownSpaceIds,
    from: start,
    to: end,
    scheduledOrInProgressOnly: scheduledOrInProgressOnly,
  );

  /// [toQuery] for the Roadmap showing [window]. The Roadmap is
  /// time-framed, so it always asks for a date window (ADR-0003): the days
  /// both [window] and the header's range cover. Null when they share no
  /// day, so nothing can match. Overview's rule is left off: within a
  /// window it would only add undated Doing items, which are Backlog items.
  WorkItemQuery? toRoadmapQuery(
    Set<String> shownSpaceIds,
    CalendarWindow window,
  ) {
    final from = _later(window.start, start);
    final to = _earlier(window.end, end);
    if (daysBetween(from, to) < 0) return null;
    return _query(shownSpaceIds, from: from, to: to);
  }

  WorkItemQuery _query(
    Set<String> shownSpaceIds, {
    DateTime? from,
    DateTime? to,
    bool scheduledOrInProgressOnly = false,
  }) => WorkItemQuery(
    spaceIds: shownSpaceIds,
    text: searchQuery,
    tags: _selectedTagsLowered,
    tagMatch: tagMatch,
    from: from,
    to: to,
    applyDoneHiding: !showDone,
    scheduledOrInProgressOnly: scheduledOrInProgressOnly,
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
    Set<String>? spaceIds,
    bool? scheduledOrInProgressOnly,
  }) => BoardFilters(
    start: identical(start, _unchanged) ? this.start : start as DateTime?,
    end: identical(end, _unchanged) ? this.end : end as DateTime?,
    searchQuery: searchQuery ?? this.searchQuery,
    hiddenStatusIds: hiddenStatusIds ?? this.hiddenStatusIds,
    selectedTags: selectedTags ?? this.selectedTags,
    tagMatch: tagMatch ?? this.tagMatch,
    sortOption: sortOption ?? this.sortOption,
    showDone: showDone ?? this.showDone,
    spaceIds: spaceIds ?? this.spaceIds,
    scheduledOrInProgressOnly:
        scheduledOrInProgressOnly ?? this.scheduledOrInProgressOnly,
  );

  static const Object _unchanged = Object();
}
