import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/work_item_picker/work_item_picker_view_model.dart';

import '../fakes/work_items.dart';

void main() {
  test('lists what the search finds for the text', () async {
    final searched = <String>[];
    final viewModel = WorkItemPickerViewModel((text) async {
      searched.add(text);
      return [workItem('kitchen', number: 5)];
    });

    await viewModel.search('kit');

    expect(searched, ['kit']);
    expect(viewModel.results.single.id, 'kitchen');
    expect(viewModel.isSearching, isFalse);
    expect(viewModel.error, isNull);
  });

  test('is searching until the search answers', () async {
    final response = Completer<List<WorkItem>>();
    final viewModel = WorkItemPickerViewModel((_) => response.future);

    final search = viewModel.search('kit');
    expect(viewModel.isSearching, isTrue);

    response.complete(const []);
    await search;
    expect(viewModel.isSearching, isFalse);
  });

  test(
    'a slower answer to an older search does not replace a newer one',
    () async {
      final responses = <String, Completer<List<WorkItem>>>{
        'k': Completer(),
        'kit': Completer(),
      };
      final viewModel = WorkItemPickerViewModel(
        (text) => responses[text]!.future,
      );

      final older = viewModel.search('k');
      final newer = viewModel.search('kit');
      responses['kit']!.complete([workItem('kitchen')]);
      await newer;
      responses['k']!.complete([workItem('kettle')]);
      await older;

      expect(viewModel.results.single.id, 'kitchen');
      expect(viewModel.isSearching, isFalse);
    },
  );

  test('a failed search says so and lists nothing', () async {
    var fail = false;
    final viewModel = WorkItemPickerViewModel((_) async {
      if (fail) throw const ApiException('Offline');
      return [workItem('kitchen')];
    });
    await viewModel.search('');

    fail = true;
    await viewModel.search('kit');

    expect(viewModel.results, isEmpty);
    expect(
      viewModel.error,
      'Could not search. Check your connection and try again.',
    );
  });
}
