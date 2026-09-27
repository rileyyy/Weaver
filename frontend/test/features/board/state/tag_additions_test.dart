import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/state/tag_additions.dart';

void main() {
  group('withTagsAdded', () {
    test('appends new tags after the existing ones', () {
      expect(withTagsAdded(['bug'], ['design', 'backend']), [
        'bug',
        'design',
        'backend',
      ]);
    });

    test('trims additions and skips blank ones', () {
      expect(withTagsAdded([], ['  design ', '   ', '']), ['design']);
    });

    test(
      'skips tags already present, ignoring case, keeping the old spelling',
      () {
        expect(withTagsAdded(['Urgent'], ['urgent', 'URGENT']), ['Urgent']);
      },
    );

    test('de-duplicates within the additions themselves', () {
      expect(withTagsAdded([], ['Design', 'design']), ['Design']);
    });

    test('leaves the existing list untouched', () {
      final existing = ['bug'];

      withTagsAdded(existing, ['design']);

      expect(existing, ['bug']);
    });
  });

  group('tagSuggestions', () {
    const available = ['Urgent', 'design', 'backend', 'Design review'];

    test('offers every tag not excluded when there is no query', () {
      expect(tagSuggestions(available, excluded: ['urgent']), [
        'design',
        'backend',
        'Design review',
      ]);
    });

    test('filters by the query, ignoring case', () {
      expect(tagSuggestions(available, excluded: [], query: ' DES '), [
        'design',
        'Design review',
      ]);
    });

    test('offers nothing when every match is excluded', () {
      expect(
        tagSuggestions(
          available,
          excluded: ['DESIGN', 'design review'],
          query: 'des',
        ),
        isEmpty,
      );
    });
  });
}
