import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/models/work_item_card.dart';
import 'package:weaver/features/board/state/board_filters.dart';

WorkItemCard _card({
  String title = 'Card',
  String? description,
  List<String> tags = const [],
  DateTime? start,
  DateTime? end,
}) => WorkItemCard(
  id: 'c',
  number: 1,
  title: title,
  parentId: 'lane',
  statusId: 'todo',
  description: description,
  tags: tags,
  startDate: start,
  endDate: end,
);

void main() {
  const none = BoardFilters();

  test('an empty filter shows everything', () {
    expect(none.cardVisible(_card()), isTrue);
    expect(none.matchesTimeWindow(null, null), isTrue);
  });

  test('changes return new values and leave the original untouched', () {
    final filtered = none
        .withSearchQuery('x')
        .withStatusToggled('done')
        .withTagToggled('Urgent')
        .withSortOption(CardSortOption.title);

    expect(none.searchQuery, isEmpty);
    expect(none.hiddenStatusIds, isEmpty);
    expect(filtered.hiddenStatusIds, {'done'});
    expect(filtered.isTagSelected('urgent'), isTrue);
    expect(filtered.sortOption, CardSortOption.title);
  });

  test('toggling twice removes the entry again', () {
    expect(
      none.withStatusToggled('done').withStatusToggled('done').hiddenStatusIds,
      isEmpty,
    );
    expect(none.withTagToggled('a').withTagToggled('A').selectedTags, isEmpty);
  });

  test('the exposed sets are read-only', () {
    final filtered = none.withStatusToggled('done');

    expect(() => filtered.hiddenStatusIds.add('todo'), throwsUnsupportedError);
  });

  test(
    'time bounds overlap by calendar day and missing dates are open-ended',
    () {
      final filter = none.withTimeRange(
        start: DateTime(2026, 9, 10),
        end: DateTime(2026, 9, 20, 9),
      );

      expect(filter.matchesTimeWindow(DateTime(2026, 9, 20, 23), null), isTrue);
      expect(filter.matchesTimeWindow(DateTime(2026, 9, 21), null), isFalse);
      expect(filter.matchesTimeWindow(null, DateTime(2026, 9, 10)), isTrue);
      expect(filter.matchesTimeWindow(null, DateTime(2026, 9, 9)), isFalse);
      expect(filter.matchesTimeWindow(null, null), isTrue);
    },
  );

  test('clearing one bound keeps the other', () {
    final filter = none
        .withTimeRange(start: DateTime(2026, 9, 1), end: DateTime(2026, 9, 30))
        .withTimeRange(start: DateTime(2026, 9, 1));

    expect(filter.start, DateTime(2026, 9, 1));
    expect(filter.end, isNull);
  });

  test('search matches title, description or tags, ignoring case', () {
    final filter = none.withSearchQuery('  DESIGN ');

    expect(filter.cardVisible(_card(title: 'Design review')), isTrue);
    expect(filter.cardVisible(_card(description: 'needs design')), isTrue);
    expect(filter.cardVisible(_card(tags: ['designers'])), isTrue);
    expect(filter.cardVisible(_card(title: 'Build')), isFalse);
  });

  test('only the Hierarchy check looks at hidden statuses', () {
    final filter = none.withStatusToggled('todo');
    const item = HierarchyItem(
      id: 'i',
      number: 1,
      parentId: null,
      title: 'Item',
      statusId: 'todo',
    );

    expect(filter.hierarchyItemVisible(item), isFalse);
    expect(filter.cardVisible(_card()), isTrue);
  });
}
