import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/app.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/core/network/server_origin_store.dart';
import 'package:weaver/features/auth/data/auth_repository.dart';
import 'package:weaver/features/auth/data/auth_session_store.dart';
import 'package:weaver/features/auth/data/secure_token_store.dart';
import 'package:weaver/features/auth/models/auth_session.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/data/view_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/saved_view.dart';
import 'package:weaver/features/board/models/swimlane_items.dart';
import 'package:weaver/features/board/models/view_definition.dart';
import 'package:weaver/features/board/models/view_layout.dart';
import 'package:weaver/features/board/views/board_tab.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/user_kind.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_query.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/recurrence/data/recurrence_repository.dart';

import 'core/network/fakes/in_memory_server_origin_store.dart';
import 'features/board/fakes/fake_view_repository.dart';
import 'shared/fakes/work_items.dart';
import 'shared/recurrence/fake_recurrence_repository.dart';

/// Stands in for the real, network-backed [BoardRepository] so this smoke
/// test never makes an HTTP call. It never writes, so writes aren't stubbed.
class _StubBoardRepository implements BoardRepository {
  @override
  Future<List<Space>> loadSpaces() =>
      Future.value(const [Space(id: 'home', name: 'Home')]);

  @override
  Future<BoardData> loadBoard(String spaceId, String? scopeItemId) =>
      Future.value(
        BoardData(
          statuses: const [WorkItemStatus(id: 'todo', name: 'To Do', order: 0)],
          swimlanes: [
            SwimlaneItems(
              lane: workItem('epic-board', title: 'Swimlane board'),
              cards: [
                workItem(
                  'wi-1',
                  title: 'Design swimlane layout',
                  parentId: 'epic-board',
                ),
              ],
            ),
          ],
        ),
      );

  @override
  Future<List<WorkItem>> loadAllItems(String spaceId) => Future.value(const []);

  @override
  Future<List<CustomField>> loadCustomFields(String spaceId) =>
      Future.value(const []);

  final List<WorkItemQuery> queries = [];

  /// The board's default query hides old Done items; none of these are.
  @override
  Future<List<WorkItem>> queryItems(WorkItemQuery query) async {
    queries.add(query);
    return [
      for (final lane in (await loadBoard(
        query.spaceIds.first,
        null,
      )).swimlanes) ...[lane.lane, ...lane.cards],
    ];
  }

  @override
  Future<List<User>> loadUsers() => Future.value(const []);

  @override
  dynamic noSuchMethod(Invocation invocation) => throw UnimplementedError();
}

/// Never actually called: the test pre-populates a non-expired session
/// before pumping the app, so [AuthSessionStore.ensureValidSession] returns
/// without needing to refresh.
class _UnusedAuthRepository implements AuthRepository {
  @override
  Future<AuthSession> login(String username, String password) =>
      throw UnimplementedError();

  @override
  Future<AuthSession> refresh(String refreshToken) =>
      throw UnimplementedError();

  @override
  Future<void> logout(String refreshToken) => throw UnimplementedError();
}

/// In-memory stand-in for the real, platform-channel-backed
/// [SecureTokenStore] (flutter_secure_storage has no test-environment
/// implementation).
class _InMemoryTokenStore implements SecureTokenStore {
  String? _token;

  @override
  Future<String?> readRefreshToken() async => _token;

  @override
  Future<void> writeRefreshToken(String token) async => _token = token;

  @override
  Future<void> clear() async => _token = null;
}

const _hierarchyView = SavedView(
  id: 'tree',
  definition: ViewDefinition(
    name: 'Tree of things',
    layout: ViewLayout.hierarchy,
  ),
);

void main() {
  setUp(() async {
    await getIt.reset();
    configureDependencies();
    getIt
      ..unregister<BoardRepository>()
      ..registerLazySingleton<BoardRepository>(_StubBoardRepository.new)
      ..unregister<ViewRepository>()
      ..registerLazySingleton<ViewRepository>(
        () => FakeViewRepository(views: [allSpacesView, _hierarchyView]),
      )
      ..unregister<AuthRepository>()
      ..registerLazySingleton<AuthRepository>(_UnusedAuthRepository.new)
      ..unregister<SecureTokenStore>()
      ..registerLazySingleton<SecureTokenStore>(_InMemoryTokenStore.new)
      ..unregister<ServerOriginStore>()
      ..registerLazySingleton<ServerOriginStore>(
        () => InMemoryServerOriginStore('https://weaver.test'),
      )
      ..unregister<RecurrenceRepository>()
      ..registerLazySingleton<RecurrenceRepository>(
        FakeRecurrenceRepository.new,
      );

    await getIt<AuthSessionStore>().setSession(
      AuthSession(
        accessToken: 'test-access-token',
        accessTokenExpiresAtUtc: DateTime.now().toUtc().add(
          const Duration(hours: 1),
        ),
        refreshToken: 'test-refresh-token',
        user: const User(
          id: 'user-1',
          username: 'tester',
          kind: UserKind.human,
        ),
      ),
    );
  });

  testWidgets('renders the board with its swimlanes and columns', (
    WidgetTester tester,
  ) async {
    await tester.pumpWidget(const WeaverApp());
    await tester.pumpAndSettle();

    expect(find.text('Weaver'), findsOneWidget);
    // The matching filter chip lives inside the Filters dialog now, not on
    // the main screen, so only the status column header renders by default.
    expect(find.text('To Do'), findsOneWidget);
    expect(find.text('Swimlane board'), findsOneWidget);
    expect(find.text('Design swimlane layout'), findsOneWidget);
  });

  testWidgets('the Filters dialog turns Show done on and back off', (
    WidgetTester tester,
  ) async {
    await tester.pumpWidget(const WeaverApp());
    await tester.pumpAndSettle();
    final repository = getIt<BoardRepository>() as _StubBoardRepository;
    expect(repository.queries.last.applyDoneHiding, isTrue);

    await tester.tap(find.byTooltip('Filters'));
    await tester.pumpAndSettle();
    expect(
      find.text('Hiding items completed more than 14 days ago.'),
      findsOneWidget,
    );
    final asked = repository.queries.length;

    await tester.tap(find.text('Show done'));
    await tester.pumpAndSettle();
    expect(find.textContaining('Hiding items'), findsNothing);
    expect(repository.queries, hasLength(asked));

    await tester.tap(find.text('Show done'));
    await tester.pumpAndSettle();
    expect(repository.queries.last.applyDoneHiding, isTrue);
    expect(repository.queries, hasLength(asked + 1));
  });

  testWidgets('switching View opens it in its layout', (
    WidgetTester tester,
  ) async {
    await tester.pumpWidget(const WeaverApp());
    await tester.pumpAndSettle();
    expect(find.text('Everything'), findsWidgets);

    await tester.tap(find.byTooltip('Views'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Tree of things'));
    await tester.pumpAndSettle();

    final tabs = tester.widget<TabBar>(find.byType(TabBar));
    expect(tabs.controller!.index, BoardTab.hierarchy.index);
    expect(find.text('Tree of things'), findsWidgets);
  });

  testWidgets('saving the header as a new View stores it and opens it', (
    WidgetTester tester,
  ) async {
    await tester.pumpWidget(const WeaverApp());
    await tester.pumpAndSettle();
    await tester.enterText(find.byType(TextField).first, 'design');
    await tester.pumpAndSettle();

    await tester.tap(find.byTooltip('Views'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Save as new View…'));
    await tester.pumpAndSettle();
    await tester.enterText(find.widgetWithText(TextField, 'Name'), 'Designs');
    await tester.pump();
    await tester.tap(find.widgetWithText(FilledButton, 'Save'));
    await tester.pumpAndSettle();

    final views = getIt<ViewRepository>() as FakeViewRepository;
    final saved = views.views.last.definition;
    expect(saved.name, 'Designs');
    expect(saved.text, 'design');
    expect(saved.layout, ViewLayout.board);
    expect(find.text('Designs'), findsWidgets);
  });
}
