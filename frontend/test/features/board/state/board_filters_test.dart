import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/models/card_sort_option.dart';
import 'package:weaver/features/board/state/board_filters.dart';
import 'package:weaver/shared/models/tag_match.dart';

bool _asksDifferently(BoardFilters a, BoardFilters b) =>
    a.toQuery('home') != b.toQuery('home');

void main() {
  const none = BoardFilters();

  test('by default the query hides old Done items', () {
    final query = none.toQuery('home');

    expect(query.applyDoneHiding, isTrue);
    expect(query.isUnfiltered, isFalse);
  });

  test('showing Done items with no other filter asks for every item', () {
    final query = none.withShowDone(true).toQuery('home');

    expect(query.applyDoneHiding, isFalse);
    expect(query.isUnfiltered, isTrue);
  });

  test('changes return new values and leave the original untouched', () {
    final filtered = none
        .withSearchQuery('x')
        .withStatusToggled('done')
        .withTagToggled('Urgent')
        .withTagMatch(TagMatch.all)
        .withSortOption(CardSortOption.title);

    expect(none.searchQuery, isEmpty);
    expect(none.hiddenStatusIds, isEmpty);
    expect(none.tagMatch, TagMatch.any);
    expect(filtered.hiddenStatusIds, {'done'});
    expect(filtered.isTagSelected('urgent'), isTrue);
    expect(filtered.tagMatch, TagMatch.all);
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

  test('clearing one bound keeps the other', () {
    final filter = none
        .withTimeRange(start: DateTime(2026, 9, 1), end: DateTime(2026, 9, 30))
        .withTimeRange(start: DateTime(2026, 9, 1));

    expect(filter.start, DateTime(2026, 9, 1));
    expect(filter.end, isNull);
  });

  test('an inverted range is swapped, since the server rejects one', () {
    final filter = none.withTimeRange(
      start: DateTime(2026, 9, 30),
      end: DateTime(2026, 9, 1),
    );

    expect(filter.start, DateTime(2026, 9, 1));
    expect(filter.end, DateTime(2026, 9, 30));
  });

  test('the query carries the trimmed search, tags and time range', () {
    final query = none
        .withSearchQuery('  design ')
        .withTagToggled('Urgent')
        .withTimeRange(start: DateTime(2026, 9, 1), end: DateTime(2026, 9, 30))
        .toQuery('home');

    expect(query.spaceId, 'home');
    expect(query.text, 'design');
    expect(query.tags, {'urgent'});
    expect(query.from, DateTime(2026, 9, 1));
    expect(query.to, DateTime(2026, 9, 30));
    expect(query.isUnfiltered, isFalse);
  });

  test('a blank search alone adds no filter', () {
    expect(none.withSearchQuery('   ').toQuery('home'), none.toQuery('home'));
  });

  group('whether a change needs a new request', () {
    test('toggling Show done asks again', () {
      expect(_asksDifferently(none.withShowDone(true), none), isTrue);
    });

    test('hiding a status or changing the sort asks nothing new', () {
      expect(
        _asksDifferently(
          none.withStatusToggled('done').withSortOption(CardSortOption.title),
          none,
        ),
        isFalse,
      );
    });

    test('a change of search, tags or time range asks again', () {
      expect(_asksDifferently(none.withSearchQuery('x'), none), isTrue);
      expect(_asksDifferently(none.withTagToggled('x'), none), isTrue);
      expect(
        _asksDifferently(none.withTimeRange(start: DateTime(2026, 9, 1)), none),
        isTrue,
      );
    });

    test('only spaces around the search ask nothing new', () {
      final typed = none.withSearchQuery('x');

      expect(_asksDifferently(typed.withSearchQuery(' x '), typed), isFalse);
    });

    test('Any or All matters only with two or more tags', () {
      final one = none.withTagToggled('a');
      final two = one.withTagToggled('b');

      expect(_asksDifferently(one.withTagMatch(TagMatch.all), one), isFalse);
      expect(_asksDifferently(two.withTagMatch(TagMatch.all), two), isTrue);
    });
  });
}
