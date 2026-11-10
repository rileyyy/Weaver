import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/app.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/core/network/server_origin_store.dart';
import 'package:weaver/features/auth/data/auth_repository.dart';
import 'package:weaver/features/auth/data/auth_session_store.dart';
import 'package:weaver/features/auth/data/secure_token_store.dart';
import 'package:weaver/features/auth/models/auth_session.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/data/last_space_store.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/swimlane_items.dart';
import 'package:weaver/shared/models/custom_field.dart';
import 'package:weaver/shared/models/space.dart';
import 'package:weaver/shared/models/user.dart';
import 'package:weaver/shared/models/user_kind.dart';
import 'package:weaver/shared/models/work_item.dart';
import 'package:weaver/shared/models/work_item_status.dart';
import 'package:weaver/shared/recurrence/data/recurrence_repository.dart';

import 'core/network/fakes/in_memory_server_origin_store.dart';
import 'shared/fakes/memory_last_space_store.dart';
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

void main() {
  setUp(() async {
    await getIt.reset();
    configureDependencies();
    getIt
      ..unregister<BoardRepository>()
      ..registerLazySingleton<BoardRepository>(_StubBoardRepository.new)
      ..unregister<LastSpaceStore>()
      ..registerLazySingleton<LastSpaceStore>(MemoryLastSpaceStore.new)
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
}
