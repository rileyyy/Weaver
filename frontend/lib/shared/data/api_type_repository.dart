import 'package:http/http.dart' as http;
import 'package:injectable/injectable.dart';
import 'package:weaver/core/network/api_endpoint.dart';
import 'package:weaver/core/network/json_api_client.dart';
import 'package:weaver/shared/data/space_lookup_cache.dart';
import 'package:weaver/shared/data/type_repository.dart';
import 'package:weaver/shared/models/work_item_type.dart';

/// Cached per Space like statuses (see [SpaceLookupCache]): the board needs
/// a Space's Types on every scope load to colour its cards.
@LazySingleton(as: TypeRepository)
class ApiTypeRepository implements TypeRepository {
  ApiTypeRepository(http.Client client, ApiEndpoint endpoint)
    : _cache = SpaceLookupCache(
        endpoint,
        (spaceId) => JsonApiClient(client, endpoint).get(
          '/work-item-types',
          (json) => JsonApiClient.listOf(json, WorkItemType.fromJson),
          query: {'spaceId': spaceId},
          failureMessage: 'Failed to load types',
        ),
      );

  final SpaceLookupCache<WorkItemType> _cache;

  @override
  Future<List<WorkItemType>> loadTypes(String spaceId) => _cache.load(spaceId);

  @override
  void invalidate() => _cache.invalidate();
}
