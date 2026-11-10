import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/core/network/api_exception.dart';
import 'package:weaver/features/admin/agent_admin_view_model.dart';
import 'package:weaver/features/admin/agent_keys_view_model.dart';
import 'package:weaver/features/admin/lookup_admin_view_model.dart';
import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/models/lookup_value.dart';
import 'package:weaver/features/admin/space_admin_view_model.dart';
import 'package:weaver/features/admin/status_admin_view_model.dart';
import 'package:weaver/features/admin/user_admin_view_model.dart';
import 'package:weaver/features/admin/views/agents_tab.dart';
import 'package:weaver/features/admin/views/lookup_tab.dart';
import 'package:weaver/features/admin/views/spaces_tab.dart';
import 'package:weaver/features/admin/views/statuses_tab.dart';
import 'package:weaver/features/admin/views/users_tab.dart';
import 'package:weaver/shared/data/current_user.dart';
import 'package:weaver/shared/models/space.dart';

import '../fakes/fake_agent_admin_repository.dart';
import '../fakes/fake_lookup_admin_repository.dart';
import '../fakes/fake_space_admin_repository.dart';
import '../fakes/fake_status_admin_repository.dart';
import '../fakes/fake_user_admin_repository.dart';

class _SignedInAs implements CurrentUser {
  const _SignedInAs(this.currentUserId);

  @override
  final String? currentUserId;
}

Widget _host(Widget child) => MaterialApp(home: Scaffold(body: child));

const _home = Space(id: 'home', name: 'Home');
const _software = Space(id: 'software', name: 'Software');

/// The Spaces a Statuses or Priorities tab picks from, already loaded.
Future<SpaceAdminViewModel> _loadedSpaces(WidgetTester tester) async {
  final spaces = SpaceAdminViewModel(
    FakeSpaceAdminRepository(const [_home, _software]),
  );
  addTearDown(spaces.dispose);
  await spaces.load();
  return spaces;
}

void main() {
  group('SpacesTab', () {
    late FakeSpaceAdminRepository repository;
    late SpaceAdminViewModel viewModel;

    setUp(() async {
      repository = FakeSpaceAdminRepository(const [
        Space(id: 'home', name: 'Home'),
      ]);
      viewModel = SpaceAdminViewModel(repository);
      await viewModel.load();
    });

    tearDown(() => viewModel.dispose());

    testWidgets('adding a Space lists it', (tester) async {
      await tester.pumpWidget(_host(SpacesTab(viewModel: viewModel)));

      await tester.tap(find.text('Add Space'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), 'Village');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Add'));
      await tester.pumpAndSettle();

      expect(repository.calls.last, 'create Village');
      expect(find.text('Village'), findsOneWidget);
    });

    testWidgets('renaming a Space shows its new name', (tester) async {
      await tester.pumpWidget(_host(SpacesTab(viewModel: viewModel)));

      await tester.tap(find.byTooltip('Rename Home'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), 'House');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Rename'));
      await tester.pumpAndSettle();

      expect(repository.calls.last, 'rename home House');
      expect(find.text('House'), findsOneWidget);
    });

    testWidgets("shows each Space's done-hiding period", (tester) async {
      await tester.pumpWidget(_host(SpacesTab(viewModel: viewModel)));

      expect(
        find.text(
          'Done items leave the board 14 days after they are completed.',
        ),
        findsOneWidget,
      );
    });

    testWidgets('changing the done-hiding period shows the new one', (
      tester,
    ) async {
      await tester.pumpWidget(_host(SpacesTab(viewModel: viewModel)));

      await tester.tap(find.byTooltip('Change done-hiding period for Home'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), '1');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Save'));
      await tester.pumpAndSettle();

      expect(repository.calls.last, 'setDoneHidingPeriod home 1');
      expect(find.byType(AlertDialog), findsNothing);
      expect(
        find.text('Done items leave the board 1 day after they are completed.'),
        findsOneWidget,
      );
    });

    testWidgets('an out-of-range period keeps the dialog open with why', (
      tester,
    ) async {
      await tester.pumpWidget(_host(SpacesTab(viewModel: viewModel)));

      await tester.tap(find.byTooltip('Change done-hiding period for Home'));
      await tester.pumpAndSettle();
      await tester.enterText(find.byType(TextField), '0');
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Save'));
      await tester.pumpAndSettle();

      expect(find.byType(AlertDialog), findsOneWidget);
      expect(
        find.text('Enter a whole number of days from 1 to 3650.'),
        findsOneWidget,
      );
      expect(repository.calls, ['list']);
    });
  });

  group('LookupTab', () {
    testWidgets('editing a type saves its new colour', (tester) async {
      final repository = FakeLookupAdminRepository(const [
        LookupValue(id: 'task', name: 'Task', order: 0, color: '#43A047'),
      ]);
      final viewModel = LookupAdminViewModel(repository, LookupKind.types);
      addTearDown(viewModel.dispose);
      await viewModel.showSpace('home');
      final spaces = await _loadedSpaces(tester);
      await tester.pumpWidget(
        _host(LookupTab(viewModel: viewModel, spaces: spaces)),
      );

      await tester.tap(find.byTooltip('Edit Task'));
      await tester.pumpAndSettle();
      await tester.tap(find.byTooltip('#FDD835'));
      await tester.pump();
      await tester.tap(find.text('Save'));
      await tester.pumpAndSettle();

      expect(repository.calls.last, 'update types task Task #FDD835');
      expect(find.byType(CircleAvatar), findsOneWidget);
    });

    testWidgets("a Type offers its fields, and a priority doesn't", (
      tester,
    ) async {
      final repository = FakeLookupAdminRepository(const [
        LookupValue(id: 'room', name: 'Room', order: 0, color: '#43A047'),
      ]);
      final types = LookupAdminViewModel(repository, LookupKind.types);
      final priorities = LookupAdminViewModel(
        repository,
        LookupKind.priorities,
      );
      addTearDown(types.dispose);
      addTearDown(priorities.dispose);
      await types.showSpace('home');
      await priorities.showSpace('home');
      final spaces = await _loadedSpaces(tester);

      await tester.pumpWidget(
        _host(LookupTab(viewModel: types, spaces: spaces)),
      );
      expect(find.byTooltip('Room fields'), findsOneWidget);

      await tester.pumpWidget(
        _host(LookupTab(viewModel: priorities, spaces: spaces)),
      );
      expect(find.byTooltip('Room fields'), findsNothing);
    });

    testWidgets('a priority has no colour to edit', (tester) async {
      final viewModel = LookupAdminViewModel(
        FakeLookupAdminRepository(const [
          LookupValue(id: 'low', name: 'Low', order: 0),
        ]),
        LookupKind.priorities,
      );
      addTearDown(viewModel.dispose);
      await viewModel.showSpace('home');
      final spaces = await _loadedSpaces(tester);
      await tester.pumpWidget(
        _host(LookupTab(viewModel: viewModel, spaces: spaces)),
      );

      await tester.tap(find.byTooltip('Edit Low'));
      await tester.pumpAndSettle();

      expect(find.text('Rename priority'), findsOneWidget);
      expect(find.text('Colour'), findsNothing);
    });
  });

  group('StatusesTab', () {
    testWidgets('picking another Space shows its statuses', (tester) async {
      final repository = FakeStatusAdminRepository([
        adminStatus('todo', name: 'To Do'),
      ]);
      final viewModel = StatusAdminViewModel(repository);
      addTearDown(viewModel.dispose);
      await viewModel.showSpace('home');
      final spaces = await _loadedSpaces(tester);
      await tester.pumpWidget(
        _host(StatusesTab(viewModel: viewModel, spaces: spaces)),
      );

      await tester.tap(find.text('Home'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Software').last);
      await tester.pumpAndSettle();

      expect(repository.listedSpaceIds, ['home', 'software']);
      expect(viewModel.spaceId, 'software');
    });

    testWidgets('with no Space yet, says to create one and offers no add', (
      tester,
    ) async {
      final viewModel = StatusAdminViewModel(FakeStatusAdminRepository([]));
      addTearDown(viewModel.dispose);
      final spaces = SpaceAdminViewModel(FakeSpaceAdminRepository(const []));
      addTearDown(spaces.dispose);
      await spaces.load();
      await tester.pumpWidget(
        _host(StatusesTab(viewModel: viewModel, spaces: spaces)),
      );

      expect(
        find.text('Create a Space on the Spaces tab first.'),
        findsOneWidget,
      );
      final add = tester.widget<FilledButton>(
        find.widgetWithText(FilledButton, 'Add status'),
      );
      expect(add.onPressed, isNull);
    });

    testWidgets('lists the statuses with a drag handle each', (tester) async {
      final viewModel = StatusAdminViewModel(
        FakeStatusAdminRepository([
          adminStatus('todo', name: 'To Do'),
          adminStatus('done', name: 'Done'),
        ]),
      );
      addTearDown(viewModel.dispose);
      await viewModel.showSpace('home');
      final spaces = await _loadedSpaces(tester);

      await tester.pumpWidget(
        _host(StatusesTab(viewModel: viewModel, spaces: spaces)),
      );

      expect(find.text('To Do'), findsOneWidget);
      expect(find.text('Done'), findsOneWidget);
      expect(find.byIcon(Icons.drag_indicator), findsNWidgets(2));
    });

    testWidgets('does not offer deleting the last status', (tester) async {
      final viewModel = StatusAdminViewModel(
        FakeStatusAdminRepository([adminStatus('todo', name: 'To Do')]),
      );
      addTearDown(viewModel.dispose);
      await viewModel.showSpace('home');
      final spaces = await _loadedSpaces(tester);

      await tester.pumpWidget(
        _host(StatusesTab(viewModel: viewModel, spaces: spaces)),
      );

      final delete = tester.widget<IconButton>(
        find.widgetWithIcon(IconButton, Icons.delete_outline),
      );
      expect(delete.onPressed, isNull);
      expect(find.byTooltip('At least one status must remain'), findsOneWidget);
    });

    testWidgets('delete asks for a replacement among the other statuses', (
      tester,
    ) async {
      final repository = FakeStatusAdminRepository([
        adminStatus('todo', name: 'To Do'),
        adminStatus('doing', name: 'Doing'),
      ]);
      final viewModel = StatusAdminViewModel(repository);
      addTearDown(viewModel.dispose);
      await viewModel.showSpace('home');
      final spaces = await _loadedSpaces(tester);
      await tester.pumpWidget(
        _host(StatusesTab(viewModel: viewModel, spaces: spaces)),
      );

      await tester.tap(find.byTooltip('Delete Doing'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Delete'));
      await tester.pumpAndSettle();

      expect(repository.calls.single, 'delete doing -> todo');
      expect(find.text('Doing'), findsNothing);
    });
  });

  group('UsersTab', () {
    late FakeUserAdminRepository repository;
    late UserAdminViewModel viewModel;

    setUp(() async {
      repository = FakeUserAdminRepository([
        adminUser('admin', isAdmin: true),
        adminUser('carol', lockedUntil: DateTime(2026, 9, 28, 14, 30)),
      ]);
      viewModel = UserAdminViewModel(repository, const _SignedInAs('admin'));
      await viewModel.load();
    });

    tearDown(() => viewModel.dispose());

    testWidgets('marks you, admins and lockouts', (tester) async {
      await tester.pumpWidget(_host(UsersTab(viewModel: viewModel)));

      expect(find.text('You'), findsOneWidget);
      expect(find.text('Admin'), findsOneWidget);
      expect(find.text('Locked until 14:30'), findsOneWidget);
    });

    testWidgets('your own menu has no deactivate or delete', (tester) async {
      await tester.pumpWidget(_host(UsersTab(viewModel: viewModel)));

      await tester.tap(find.byTooltip('Actions for admin'));
      await tester.pumpAndSettle();

      expect(find.text('Reset password'), findsOneWidget);
      expect(find.text('Deactivate'), findsNothing);
      expect(find.text('Delete'), findsNothing);
    });

    testWidgets('deactivating asks first, then deactivates', (tester) async {
      await tester.pumpWidget(_host(UsersTab(viewModel: viewModel)));

      await tester.tap(find.byTooltip('Actions for carol'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Deactivate'));
      await tester.pumpAndSettle();
      expect(repository.calls, isEmpty);

      await tester.tap(find.widgetWithText(FilledButton, 'Deactivate'));
      await tester.pumpAndSettle();

      expect(repository.calls.single, 'setActive carol false');
      expect(find.text('Deactivated'), findsOneWidget);
    });

    testWidgets('a refused delete shows the reason', (tester) async {
      await tester.pumpWidget(_host(UsersTab(viewModel: viewModel)));
      repository.error = const ApiException(
        "User 'carol' has written 2 comment(s) (409).",
        statusCode: 409,
      );

      await tester.tap(find.byTooltip('Actions for carol'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Delete'));
      await tester.pumpAndSettle();
      await tester.tap(find.widgetWithText(FilledButton, 'Delete'));
      await tester.pumpAndSettle();

      expect(find.textContaining('has written'), findsOneWidget);
      expect(find.text('carol'), findsOneWidget);
    });
  });
  group('AgentsTab', () {
    late FakeAgentAdminRepository repository;
    late AgentAdminViewModel viewModel;

    setUp(() async {
      repository = FakeAgentAdminRepository(
        [
          agentAccount(
            'a1',
            name: 'Claude Code (desktop)',
            description: 'On the shared laptop',
          ),
          agentAccount('a2', name: 'Retired bot', isActive: false),
        ],
        keys: {
          'a1': [apiKey('k1', label: 'laptop')],
        },
      );
      viewModel = AgentAdminViewModel(repository);
      await viewModel.load();
    });

    tearDown(() => viewModel.dispose());

    testWidgets('lists agents with their description and state', (
      tester,
    ) async {
      await tester.pumpWidget(_host(AgentsTab(viewModel: viewModel)));

      expect(find.text('Claude Code (desktop)'), findsOneWidget);
      expect(find.text('On the shared laptop'), findsOneWidget);
      expect(find.text('Deactivated'), findsOneWidget);
    });

    testWidgets('adding an agent takes a free-text name and a description', (
      tester,
    ) async {
      await tester.pumpWidget(_host(AgentsTab(viewModel: viewModel)));

      await tester.tap(find.text('Add agent'));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.widgetWithText(TextField, 'Name'),
        'Codex (CI)',
      );
      await tester.enterText(
        find.widgetWithText(TextField, 'Description (optional)'),
        'Runs nightly',
      );
      await tester.pump();
      await tester.tap(find.widgetWithText(FilledButton, 'Add'));
      await tester.pumpAndSettle();

      expect(repository.calls.single, 'create Codex (CI) Runs nightly');
      expect(find.text('Codex (CI)'), findsOneWidget);
    });

    testWidgets('deactivating says keys are kept, then deactivates', (
      tester,
    ) async {
      await tester.pumpWidget(_host(AgentsTab(viewModel: viewModel)));

      await tester.tap(find.byTooltip('Actions for Claude Code (desktop)'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Deactivate'));
      await tester.pumpAndSettle();
      expect(find.textContaining('doesn’t revoke its keys'), findsOneWidget);
      expect(repository.calls, isEmpty);

      await tester.tap(find.widgetWithText(FilledButton, 'Deactivate'));
      await tester.pumpAndSettle();

      expect(repository.calls.single, 'setActive a1 false');
      expect(find.text('Deactivated'), findsNWidgets(2));
    });

    testWidgets('reactivating needs no confirmation', (tester) async {
      await tester.pumpWidget(_host(AgentsTab(viewModel: viewModel)));

      await tester.tap(find.byTooltip('Actions for Retired bot'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Reactivate'));
      await tester.pumpAndSettle();

      expect(repository.calls.single, 'setActive a2 true');
      expect(find.text('Deactivated'), findsNothing);
    });

    testWidgets('tapping an agent opens its keys', (tester) async {
      await getIt.reset();
      addTearDown(getIt.reset);
      getIt.registerFactoryParam<AgentKeysViewModel, String, void>(
        (agentId, _) => AgentKeysViewModel(repository, agentId),
      );
      await tester.pumpWidget(_host(AgentsTab(viewModel: viewModel)));

      await tester.tap(find.text('Claude Code (desktop)'));
      await tester.pumpAndSettle();

      expect(find.text('Keys for Claude Code (desktop)'), findsOneWidget);
      expect(find.text('laptop'), findsOneWidget);
    });
  });
}
