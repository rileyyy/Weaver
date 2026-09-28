import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/core/di/injection.dart';
import 'package:weaver/features/admin/admin_view.dart';
import 'package:weaver/features/admin/lookup_admin_view_model.dart';
import 'package:weaver/features/admin/models/lookup_kind.dart';
import 'package:weaver/features/admin/models/lookup_value.dart';
import 'package:weaver/features/admin/status_admin_view_model.dart';
import 'package:weaver/features/admin/user_admin_view_model.dart';
import 'package:weaver/shared/data/current_user.dart';

import '../fakes/fake_lookup_admin_repository.dart';
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
    getIt
      ..registerFactory(() => StatusAdminViewModel(statuses))
      ..registerFactoryParam<LookupAdminViewModel, LookupKind, void>(
        (kind, _) => LookupAdminViewModel(lookups, kind),
      )
      ..registerFactory(
        () => UserAdminViewModel(users, const _SignedInAs('alice')),
      );
  });

  tearDown(getIt.reset);

  testWidgets('has a tab for each thing an admin manages, each loaded', (
    tester,
  ) async {
    await tester.pumpWidget(const MaterialApp(home: AdminView()));
    await tester.pumpAndSettle();

    for (final tab in ['Statuses', 'Priorities', 'Layers', 'Users']) {
      expect(find.widgetWithText(Tab, tab), findsOneWidget);
    }
    expect(find.text('To Do'), findsOneWidget);
    expect(lookups.calls, containsAll(['list priorities', 'list layers']));

    await tester.tap(find.widgetWithText(Tab, 'Users'));
    await tester.pumpAndSettle();

    expect(find.text('alice'), findsOneWidget);
  });
}
