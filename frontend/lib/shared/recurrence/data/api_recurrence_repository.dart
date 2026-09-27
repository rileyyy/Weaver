import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/shared/recurrence/data/recurrence_repository.dart';
import 'package:weaver/shared/recurrence/models/recurrence_draft.dart';
import 'package:weaver/shared/recurrence/models/work_item_recurrence.dart';

@LazySingleton(as: RecurrenceRepository)
class ApiRecurrenceRepository implements RecurrenceRepository {
  ApiRecurrenceRepository(
    http.Client client,
    @Named('apiBaseUrl') String baseUrl,
  ) : _api = JsonApiClient(client, baseUrl);

  final JsonApiClient _api;

  static WorkItemRecurrence _recurrence(Object? json) =>
      WorkItemRecurrence.fromJson(json as Map<String, dynamic>);

  @override
  Future<WorkItemRecurrence?> get(String workItemId) => _api.get(
    '/work-items/$workItemId/recurrence',
    // 204 No Content when the item doesn't repeat.
    (json) => json == null ? null : _recurrence(json),
    failureMessage: 'Failed to load repeat settings',
  );

  @override
  Future<List<WorkItemRecurrence>> list() => _api.get(
    '/recurrences',
    (json) => JsonApiClient.listOf(json, WorkItemRecurrence.fromJson),
    failureMessage: 'Failed to load repeating items',
  );

  @override
  Future<WorkItemRecurrence> save(String workItemId, RecurrenceDraft draft) =>
      _api.put(
        '/work-items/$workItemId/recurrence',
        draft.toJson(),
        _recurrence,
        failureMessage: 'Failed to save repeat settings',
      );

  @override
  Future<void> remove(String workItemId) => _api.delete(
    '/work-items/$workItemId/recurrence',
    failureMessage: 'Failed to stop repeating',
  );
}
