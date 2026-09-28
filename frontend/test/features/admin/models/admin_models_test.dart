import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/admin/models/admin_status.dart';
import 'package:weaver/features/admin/models/admin_user.dart';
import 'package:weaver/features/admin/models/status_category.dart';
import 'package:weaver/features/admin/models/user_action.dart';
import 'package:weaver/shared/models/user_kind.dart';

AdminUser _user({
  bool isAdmin = false,
  bool isActive = true,
  DateTime? lockedUntil,
}) => AdminUser(
  id: 'u1',
  username: 'alice',
  kind: UserKind.human,
  isAdmin: isAdmin,
  isActive: isActive,
  lockedUntil: lockedUntil,
);

void main() {
  group('AdminStatus', () {
    test('parses the category and keeps the colour text', () {
      final status = AdminStatus.fromJson({
        'id': 's1',
        'name': 'Review',
        'order': 3,
        'category': 'Doing',
        'color': '#00897B',
      });

      expect(status.category, StatusCategory.doing);
      expect(status.color, '#00897B');
    });

    test('rejects an unknown category', () {
      expect(
        () => StatusCategory.fromWire('Blocked'),
        throwsA(isA<FormatException>()),
      );
    });
  });

  group('AdminUser', () {
    test('parses a lockout into local time', () {
      final user = AdminUser.fromJson({
        'id': 'u1',
        'username': 'alice',
        'kind': 'Human',
        'isAdmin': true,
        'isActive': false,
        'lockedUntilUtc': '2026-09-28T12:00:00Z',
        'createdAtUtc': '2026-09-01T12:00:00Z',
      });

      expect(user.isAdmin, isTrue);
      expect(user.isActive, isFalse);
      expect(user.isLockedOut, isTrue);
      expect(user.lockedUntil, DateTime.utc(2026, 9, 28, 12).toLocal());
    });

    test('is not locked out without a lockout time', () {
      final user = AdminUser.fromJson({
        'id': 'u1',
        'username': 'alice',
        'kind': 'Human',
        'isAdmin': false,
        'isActive': true,
        'lockedUntilUtc': null,
        'createdAtUtc': '2026-09-01T12:00:00Z',
      });

      expect(user.isLockedOut, isFalse);
    });
  });

  group('UserAction.availableFor', () {
    test('offers unlock only while locked out', () {
      expect(
        UserAction.availableFor(_user(), isCurrentUser: false),
        isNot(contains(UserAction.unlock)),
      );
      expect(
        UserAction.availableFor(
          _user(lockedUntil: DateTime(2026)),
          isCurrentUser: false,
        ),
        contains(UserAction.unlock),
      );
    });

    test('offers granting or revoking admin depending on the user', () {
      expect(
        UserAction.availableFor(_user(), isCurrentUser: false),
        contains(UserAction.grantAdmin),
      );
      expect(
        UserAction.availableFor(_user(isAdmin: true), isCurrentUser: false),
        allOf(
          contains(UserAction.revokeAdmin),
          isNot(contains(UserAction.grantAdmin)),
        ),
      );
    });

    test(
      'offers deactivating an active user and reactivating an inactive one',
      () {
        expect(
          UserAction.availableFor(_user(), isCurrentUser: false),
          allOf(
            contains(UserAction.deactivate),
            isNot(contains(UserAction.reactivate)),
          ),
        );
        expect(
          UserAction.availableFor(_user(isActive: false), isCurrentUser: false),
          allOf(
            contains(UserAction.reactivate),
            isNot(contains(UserAction.deactivate)),
          ),
        );
      },
    );

    test('never offers deactivating or deleting your own account', () {
      final actions = UserAction.availableFor(_user(), isCurrentUser: true);

      expect(actions, isNot(contains(UserAction.deactivate)));
      expect(actions, isNot(contains(UserAction.delete)));
      expect(actions, contains(UserAction.resetPassword));
    });
  });
}
