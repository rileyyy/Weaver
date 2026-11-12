import 'package:flutter_test/flutter_test.dart';
import 'package:weaver/features/admin/models/space_history_entry.dart';
import 'package:weaver/shared/models/user_kind.dart';

SpaceHistoryEntry _entry(
  String field, {
  Object? oldValue,
  Object? newValue,
  String? accountName = 'admin',
  String? accountKind = 'Human',
  String? apiKeyLabel,
  String? actorLabel,
}) => SpaceHistoryEntry.fromJson({
  'id': 1,
  'spaceId': 'home',
  'changedAtUtc': '2026-11-12T09:00:00Z',
  'accountId': 'u1',
  'accountName': accountName,
  'accountKind': accountKind,
  'apiKeyId': null,
  'apiKeyLabel': apiKeyLabel,
  'actorLabel': actorLabel,
  'field': field,
  'oldValue': oldValue,
  'newValue': newValue,
});

void main() {
  test('reads who made the change and when', () {
    final entry = _entry('created', newValue: 'Home');

    expect(entry.actorName, 'admin');
    expect(entry.accountKind, UserKind.human);
    expect(entry.isAgent, isFalse);
    expect(entry.changedAt, DateTime.utc(2026, 11, 12, 9).toLocal());
    expect(entry.actorDetail, isNull);
  });

  test('describes each kind of change', () {
    expect(_entry('created', newValue: 'Home').change, 'Created "Home"');
    expect(
      _entry('name', oldValue: 'Home', newValue: 'House').change,
      'Renamed "Home" → "House"',
    );
    expect(
      _entry('doneHidingDays', oldValue: 14, newValue: 1).change,
      'Done-hiding period: 14 days → 1 day',
    );
  });

  test('an unknown field still says what changed', () {
    expect(_entry('colour', newValue: 'red').change, 'colour: none → red');
  });

  test('a change with no account is Weaver itself', () {
    final entry = _entry('created', accountName: null, accountKind: null);

    expect(entry.actorName, 'Weaver');
  });

  test('names the label and key an agent sent', () {
    final entry = _entry(
      'name',
      accountKind: 'Agent',
      apiKeyLabel: 'nightly',
      actorLabel: 'triage',
    );

    expect(entry.isAgent, isTrue);
    expect(entry.actorDetail, 'triage · key "nightly"');
  });
}
