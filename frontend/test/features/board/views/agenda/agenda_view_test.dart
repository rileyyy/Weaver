import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/board/agenda_view_model.dart';
import 'package:weaver/features/board/board_view_model.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/views/agenda/agenda_view.dart';
import 'package:weaver/shared/data/work_item_store.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_query.dart';
import 'package:weaver/shared/models/work_item_status.dart';

import '../../../../shared/fakes/work_items.dart';
import '../../fakes/fake_calendar_repository.dart';
import '../../fakes/fake_view_repository.dart';

const _home = Space(id: 'home', name: 'Home');
const _village = Space(id: 'village', name: 'Village');

/// The server's today in these tests: Wednesday 11 November 2026.
final _today = DateTime(2026, 11, 11);

class _AgendaRepository implements BoardRepository {
  _AgendaRepository(Iterable<WorkItem> items, {this.spaces = const [_home]}) {
    for (final item in items) {
      server[item.id] = item;
    }
  }

  final List<Space> spaces;
  final Map<String, WorkItem> server = {};
  final List<String> statusChanges = [];

  @override
  Future<List<Space>> loadSpaces() async => spaces;

  @override
  Future<BoardData> loadBoard(String spaceId, String? scopeItemId) async =>
      BoardData(
        statuses: [
          WorkItemStatus(id: '$spaceId-todo', name: 'To Do', order: 0),
          WorkItemStatus(id: '$spaceId-done', name: 'Done', order: 1),
        ],
        swimlanes: const [],
        types: const [],
      );

  @override
  Future<List<WorkItem>> loadAllItems(String spaceId) async => [
    for (final item in server.values)
      if (item.spaceId == spaceId) item,
  ];

  @override
  Future<List<CustomField>> loadCustomFields(String spaceId) async => const [];

  @override
  Future<List<WorkItem>> queryItems(WorkItemQuery query) async => [
    for (final item in server.values)
      if (query.spaceIds.contains(item.spaceId)) item,
  ];

  @override
  Future<List<User>> loadUsers() async => const [];

  @override
  Future<WorkItem> getItem(String id) async => server[id]!;

  @override
  Future<WorkItem> changeStatus(
    String cardId,
    String newStatusId, {
    required String? afterId,
    required int expectedVersion,
  }) async {
    statusChanges.add('$cardId->$newStatusId');
    final item = server[cardId]!;
    return server[cardId] = withVersion(
      item.withStatus(newStatusId),
      item.version + 1,
    );
  }

  @override
  dynamic noSuchMethod(Invocation invocation) => throw UnimplementedError();
}

WorkItem _dated(
  String id, {
  int number = 1,
  DateTime? start,
  DateTime? end,
  String spaceId = 'home',
}) => workItem(
  id,
  number: number,
  title: id,
  spaceId: spaceId,
  statusId: '$spaceId-todo',
  startDate: start,
  endDate: end ?? start,
);

/// Pumps the Agenda at a phone's width.
Future<_AgendaRepository> _pumpAgenda(
  WidgetTester tester,
  List<WorkItem> items, {
  List<Space> spaces = const [_home],
  void Function(String id)? onItemOpened,
}) async {
  tester.view.physicalSize = const Size(390, 844);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.reset);
  final repository = _AgendaRepository(items, spaces: spaces);
  final board = BoardViewModel(
    repository,
    WorkItemStore(),
    FakeViewRepository(),
  );
  await board.load();
  await board.loadHierarchy();
  final agenda = AgendaViewModel(FakeCalendarRepository(_today));
  await agenda.load();
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: ListenableBuilder(
          listenable: board.itemChanges,
          builder: (context, _) => AgendaView(
            boardViewModel: board,
            viewModel: agenda,
            onItemOpened: onItemOpened ?? (_) {},
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
  return repository;
}

/// Expects [texts] to show, each below the one before.
void _expectTopToBottom(WidgetTester tester, List<String> texts) {
  final tops = [
    for (final text in texts) tester.getTopLeft(find.text(text)).dy,
  ];
  for (var i = 1; i < tops.length; i++) {
    expect(
      tops[i],
      greaterThan(tops[i - 1]),
      reason: '"${texts[i]}" should be below "${texts[i - 1]}"',
    );
  }
}

void main() {
  testWidgets('groups dated items under Overdue, Today, Tomorrow and later '
      'days at phone width', (tester) async {
    await _pumpAgenda(tester, [
      workItem('undated', statusId: 'home-todo'),
      _dated('saturday', start: DateTime(2026, 11, 14)),
      _dated('tomorrow', start: DateTime(2026, 11, 12)),
      _dated('today', start: DateTime(2026, 11, 11)),
      _dated('last week', start: DateTime(2026, 11, 4)),
    ]);

    _expectTopToBottom(tester, [
      'Overdue',
      '#1 last week',
      'Today',
      '#1 today',
      'Tomorrow',
      '#1 tomorrow',
      'Sat 14 Nov',
      '#1 saturday',
    ]);
    expect(find.text('#1 undated'), findsNothing);
    expect(tester.takeException(), isNull);
  });

  testWidgets('shows a multi-day item once, with its range', (tester) async {
    await _pumpAgenda(tester, [
      _dated('trip', start: DateTime(2026, 11, 9), end: DateTime(2026, 11, 13)),
    ]);

    expect(find.text('#1 trip'), findsOneWidget);
    expect(find.text('Mon 9 Nov – Fri 13 Nov'), findsOneWidget);
  });

  testWidgets('names the Space of each item while several show', (
    tester,
  ) async {
    await _pumpAgenda(
      tester,
      [_dated('fence', start: DateTime(2026, 11, 11), spaceId: 'village')],
      spaces: const [_home, _village],
    );

    expect(find.text('Village'), findsOneWidget);
  });

  testWidgets('tapping an item opens it', (tester) async {
    final opened = <String>[];
    await _pumpAgenda(tester, [
      _dated('today', start: DateTime(2026, 11, 11)),
    ], onItemOpened: opened.add);

    await tester.tap(find.text('#1 today'));

    expect(opened, ['today']);
  });

  testWidgets("an item's status can be changed from the list", (tester) async {
    final repository = await _pumpAgenda(tester, [
      _dated('today', start: DateTime(2026, 11, 11)),
    ]);

    await tester.tap(find.byTooltip('Change status'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Done').last);
    await tester.pumpAndSettle();

    expect(repository.statusChanges, ['today->home-done']);
    expect(
      find.descendant(
        of: find.byTooltip('Change status'),
        matching: find.text('Done'),
      ),
      findsOneWidget,
    );
  });

  testWidgets('says so when nothing is dated', (tester) async {
    await _pumpAgenda(tester, [workItem('undated', statusId: 'home-todo')]);

    expect(
      find.text('Nothing is dated. Undated items are in the Backlog.'),
      findsOneWidget,
    );
  });
}
