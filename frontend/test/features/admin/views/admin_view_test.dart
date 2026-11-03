import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/admin/admin_view.dart';
import 'package:weaver/features/admin/agent_admin_view_model.dart';
import 'package:weaver/features/admin/lookup_admin_view_model.dart';
import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/models/lookup_value.dart';
import 'package:weaver/features/admin/server_settings_admin_view_model.dart';
import 'package:weaver/features/admin/space_admin_view_model.dart';
import 'package:weaver/features/admin/status_admin_view_model.dart';
import 'package:weaver/features/admin/user_admin_view_model.dart';
import 'package:weaver/shared/data/current_user.dart';
import 'package:weaver/shared/models/space.dart';

import '../fakes/fake_agent_admin_repository.dart';
import '../fakes/fake_lookup_admin_repository.dart';
import '../fakes/fake_server_settings_admin_repository.dart';
import '../fakes/fake_space_admin_repository.dart';
import '../fakes/fake_status_admin_repository.dart';
import '../fakes/fake_user_admin_repository.dart';

class _SignedInAs implements CurrentUser {
  const _SignedInAs(this.currentUserId);

  @override
  final String? currentUserId;
}

void main() {
  late FakeLookupAdminRepository lookups;

  setUp(() async {
    await getIt.reset();
    lookups = FakeLookupAdminRepository(const [
      LookupValue(id: 'low', name: 'Low', order: 0),
    ]);
    final statuses = FakeStatusAdminRepository([
      adminStatus('todo', name: 'To Do'),
    ]);
    final users = FakeUserAdminRepository([adminUser('alice', isAdmin: true)]);
    final agents = FakeAgentAdminRepository([
      agentAccount('a1', name: 'Claude Code (desktop)'),
    ]);
    final spaces = FakeSpaceAdminRepository(const [
      Space(id: 'home', name: 'Home'),
    ]);
    getIt
      ..registerFactory(() => SpaceAdminViewModel(spaces))
      ..registerFactory(() => StatusAdminViewModel(statuses))
      ..registerFactoryParam<LookupAdminViewModel, LookupKind, void>(
        (kind, _) => LookupAdminViewModel(lookups, kind),
      )
      ..registerFactory(
        () => UserAdminViewModel(users, const _SignedInAs('alice')),
      )
      ..registerFactory(() => AgentAdminViewModel(agents))
      ..registerFactory(
        () => ServerSettingsAdminViewModel(
          FakeServerSettingsAdminRepository(timeZoneId: 'Europe/Berlin'),
        ),
      );
  });

  tearDown(getIt.reset);

  testWidgets('has a tab for each thing an admin manages, each loaded', (
    tester,
  ) async {
    await tester.pumpWidget(const MaterialApp(home: AdminView()));
    await tester.pumpAndSettle();

    for (final tab in [
      'Spaces',
      'Statuses',
      'Priorities',
      'Layers',
      'Users',
      'Agents',
      'Server',
    ]) {
      expect(find.widgetWithText(Tab, tab), findsOneWidget);
    }
    expect(find.text('Home'), findsOneWidget);

    await tester.tap(find.widgetWithText(Tab, 'Statuses'));
    await tester.pumpAndSettle();

    expect(find.text('To Do'), findsOneWidget);
    expect(lookups.calls, containsAll(['list priorities', 'list layers']));

    await tester.tap(find.widgetWithText(Tab, 'Users'));
    await tester.pumpAndSettle();

    expect(find.text('alice'), findsOneWidget);

    await tester.tap(find.widgetWithText(Tab, 'Agents'));
    await tester.pumpAndSettle();

    expect(find.text('Claude Code (desktop)'), findsOneWidget);

    await tester.tap(find.widgetWithText(Tab, 'Server'));
    await tester.pumpAndSettle();

    expect(find.text('Europe/Berlin'), findsOneWidget);
  });
}
