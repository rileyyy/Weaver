import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/board/backlog_view_model.dart';
import 'package:weaver/features/board/models/backlog_group.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/space.dart';

import '../../shared/fakes/work_items.dart';
import 'fakes/fake_backlog_repository.dart';

final _nov = DateTime(2026, 11, 14);

/// Each group as its parent's title (null for top level), then its items'.
List<List<String?>> _shape(List<BacklogGroup> groups) => [
  for (final group in groups)
    [group.parent?.title, for (final item in group.items) item.title],
];

const _homeSpace = Space(id: 'home', name: 'Home');
const _workSpace = Space(id: 'work', name: 'Work');

void main() {
  late FakeBacklogRepository repository;
  late WorkItemStore store;
  late BacklogViewModel viewModel;

  setUp(() {
    repository = FakeBacklogRepository([
      workItem('house', number: 1, title: 'House', startDate: _nov),
      workItem('paint', number: 2, parentId: 'house', title: 'Paint hall'),
      workItem('shed', number: 3, title: 'Shed'),
      workItem('roof', number: 4, parentId: 'shed', title: 'Fix roof'),
      workItem('bench', number: 5, title: 'Garden bench'),
      workItem('away', number: 6, spaceId: 'work', title: 'Other Space'),
    ]);
    store = WorkItemStore();
    viewModel = BacklogViewModel(repository, store);
  });

  tearDown(() => viewModel.dispose());

  test("asks the server for only the Space's Backlog", () async {
    await viewModel.load(const [_homeSpace]);

    final query = repository.queries.single;
    expect(query.spaceIds, {'home'});
    expect(query.backlogOnly, isTrue);
  });

  test('groups items by parent, top-level items first, then parents by '
      'title', () async {
    await viewModel.load(const [_homeSpace]);

    expect(_shape(viewModel.groups), [
      [null, 'Shed', 'Garden bench'],
      ['House', 'Paint hall'],
      ['Shed', 'Fix roof'],
    ]);
  });

  test('across several Spaces, gives each its own top-level group', () async {
    await viewModel.load(const [_homeSpace, _workSpace]);

    expect(
      [
        for (final group in viewModel.groups)
          if (group.parent == null) group.space?.name,
      ],
      ['Home', 'Work'],
    );
    expect(viewModel.groups[1].items.single.title, 'Other Space');
  });

  test('loads only the parents it does not hold yet', () async {
    store.putAll([repository.server['house']!], asOf: store.revision);

    await viewModel.load(const [_homeSpace]);

    expect(repository.itemLoads, isEmpty);
    expect(viewModel.groups[1].parent?.title, 'House');
  });

  test('an empty Backlog has no groups', () async {
    repository.done.addAll(['paint', 'shed', 'roof', 'bench']);

    await viewModel.load(const [_homeSpace]);

    expect(viewModel.groups, isEmpty);
    expect(viewModel.loadError, isNull);
  });

  test('setting dates sends them with the version and moves the item out of '
      'the Backlog', () async {
    await viewModel.load(const [_homeSpace]);

    final saved = await viewModel.setDates('bench', _nov, _nov);
    await pumpEventQueue();

    expect(saved, isTrue);
    expect(repository.reschedules, ['bench@1']);
    expect(repository.server['bench']!.startDate, _nov);
    expect(_shape(viewModel.groups).first, [null, 'Shed']);
  });

  test('dates set anywhere else move the item out too', () async {
    await viewModel.load(const [_homeSpace]);

    await store.write(
      'paint',
      send: (version) => repository.rescheduleItem(
        'paint',
        _nov,
        null,
        expectedVersion: version,
      ),
      reload: repository.getItem,
    );
    await pumpEventQueue();

    expect(
      viewModel.groups.map((group) => group.parent?.title),
      isNot(contains('House')),
    );
  });

  test('an edit that leaves the item undated keeps it', () async {
    await viewModel.load(const [_homeSpace]);

    await store.write(
      'bench',
      optimistic: (item) => item.tagged(['wood']),
      send: (_) async => repository.server['bench'] = withVersion(
        repository.server['bench']!.tagged(['wood']),
        2,
      ),
      reload: repository.getItem,
    );
    await pumpEventQueue();

    final bench = viewModel.groups.first.items.last;
    expect(bench.title, 'Garden bench');
    expect(bench.tags, ['wood']);
  });

  test('a failed date change keeps the item and says why', () async {
    await viewModel.load(const [_homeSpace]);
    repository.rescheduleError = const ApiException('down');

    final saved = await viewModel.setDates('bench', _nov, _nov);

    expect(saved, isFalse);
    expect(viewModel.actionError, isNotNull);
    final bench = viewModel.groups.first.items.last;
    expect(bench.startDate, isNull);
  });

  test('a failed load says so', () async {
    repository.queryError = const ApiException('down');

    await viewModel.load(const [_homeSpace]);

    expect(viewModel.loadError, isNotNull);
    expect(viewModel.isLoading, isFalse);
  });

  test(
    "a slower answer for the previous Space can't replace the new one",
    () async {
      final pending = repository.pendingQueries = [];
      final first = viewModel.load(const [_homeSpace]);
      final second = viewModel.load(const [_workSpace]);

      pending[1].complete([repository.server['away']!]);
      await second;
      pending[0].complete([repository.server['bench']!]);
      await first;

      expect(_shape(viewModel.groups), [
        [null, 'Other Space'],
      ]);
    },
  );

  test(
    'a save asks nothing once refreshing stops, until the next load',
    () async {
      await viewModel.load(const [_homeSpace]);
      viewModel.stopRefreshing();
      repository.queries.clear();

      await viewModel.setDates('bench', _nov, _nov);
      await pumpEventQueue();
      final whileStopped = repository.queries.length;
      await viewModel.load(const [_homeSpace]);
      await viewModel.setDates('paint', _nov, _nov);
      await pumpEventQueue();

      expect(whileStopped, 0);
      expect(repository.queries, hasLength(greaterThan(1)));
    },
  );

  test('nothing is asked after it is disposed', () async {
    final other = BacklogViewModel(repository, store);
    await other.load(const [_homeSpace]);
    other.dispose();
    repository.queries.clear();

    await store.write(
      'bench',
      send: (version) => repository.rescheduleItem(
        'bench',
        _nov,
        _nov,
        expectedVersion: version,
      ),
      reload: repository.getItem,
    );
    await pumpEventQueue();

    expect(repository.queries, isEmpty);
  });
}
