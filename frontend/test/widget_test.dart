import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/app.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/auth/data/auth_repository.dart';
import 'package:weaver/features/auth/data/auth_session_store.dart';
import 'package:weaver/features/auth/data/secure_token_store.dart';
import 'package:weaver/features/auth/models/auth_session.dart';
import 'package:weaver/features/auth/models/auth_user.dart';
import 'package:weaver/features/board/data/board_repository.dart';
import 'package:weaver/features/board/models/board_data.dart';
import 'package:weaver/features/board/models/board_status.dart';
import 'package:weaver/features/board/models/hierarchy_item.dart';
import 'package:weaver/features/board/models/swimlane.dart';
import 'package:weaver/features/board/models/work_item_card.dart';

/// Stands in for the real, network-backed [BoardRepository] so this smoke
/// test never makes an HTTP call.
class _StubBoardRepository implements BoardRepository {
  @override
  Future<String?> loadRootScopeItemId() => Future.value();

  @override
  Future<BoardData> loadBoard(String? scopeItemId) => Future.value(
    const BoardData(
      statuses: [BoardStatus(id: 'todo', name: 'To Do', order: 0)],
      swimlanes: [
        Swimlane(
          parentId: 'epic-board',
          title: 'Swimlane board',
          cards: [
            WorkItemCard(
              id: 'wi-1',
              number: 1,
              title: 'Design swimlane layout',
              parentId: 'epic-board',
              statusId: 'todo',
            ),
          ],
        ),
      ],
    ),
  );

  @override
  Future<List<HierarchyItem>> loadAllItems() => Future.value(const []);

  @override
  Future<List<AuthUser>> loadUsers() => Future.value(const []);

  @override
  Future<void> changeStatus(String cardId, String newStatusId) =>
      Future.value();

  @override
  Future<void> reparentItem(String itemId, String newParentId) =>
      Future.value();

  @override
  Future<void> rescheduleItem(
    String itemId,
    DateTime? startDate,
    DateTime? endDate,
  ) => Future.value();

  @override
  Future<void> createWorkItem({
    required String title,
    String? description,
    required String? parentId,
    required String statusId,
  }) => Future.value();
}

/// Never actually called: the test pre-populates a non-expired session
/// before pumping the app, so [AuthSessionStore.ensureValidSession] returns
/// without needing to refresh.
class _UnusedAuthRepository implements AuthRepository {
  @override
  Future<AuthSession> login(String username, String password) =>
      throw UnimplementedError();

  @override
  Future<AuthSession> register(String username, String password) =>
      throw UnimplementedError();

  @override
  Future<AuthSession> refresh(String refreshToken) => throw UnimplementedError();

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
      ..unregister<AuthRepository>()
      ..registerLazySingleton<AuthRepository>(_UnusedAuthRepository.new)
      ..unregister<SecureTokenStore>()
      ..registerLazySingleton<SecureTokenStore>(_InMemoryTokenStore.new);

    await getIt<AuthSessionStore>().setSession(
      AuthSession(
        accessToken: 'test-access-token',
        accessTokenExpiresAtUtc: DateTime.now().toUtc().add(const Duration(hours: 1)),
        refreshToken: 'test-refresh-token',
        user: const AuthUser(id: 'user-1', username: 'tester', kind: UserKind.human),
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
