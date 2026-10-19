// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format width=80

// **************************************************************************
// InjectableConfigGenerator
// **************************************************************************

// ignore_for_file: type=lint
// coverage:ignore-file

// ignore_for_file: no_leading_underscores_for_library_prefixes

import 'package:get_it/get_it.dart' as _i174;
import 'package:http/http.dart' as _i519;
import 'package:injectable/injectable.dart' as _i526;
import 'package:weaver/core/di/network_module.dart' as _i561;
import 'package:weaver/core/network/api_endpoint.dart' as _i202;
import 'package:weaver/core/network/flutter_secure_server_origin_store.dart'
    as _i781;
import 'package:weaver/core/network/server_origin_store.dart' as _i936;
import 'package:weaver/core/network/server_settings.dart' as _i577;
import 'package:weaver/features/admin/data/api_lookup_admin_repository.dart'
    as _i427;
import 'package:weaver/features/admin/data/api_status_admin_repository.dart'
    as _i550;
import 'package:weaver/features/admin/data/api_user_admin_repository.dart'
    as _i756;
import 'package:weaver/features/admin/data/lookup_admin_repository.dart'
    as _i377;
import 'package:weaver/features/admin/data/status_admin_repository.dart'
    as _i224;
import 'package:weaver/features/admin/data/user_admin_repository.dart' as _i290;
import 'package:weaver/features/admin/lookup_admin_view_model.dart' as _i281;
import 'package:weaver/features/admin/models/lookup_kind.dart' as _i54;
import 'package:weaver/features/admin/status_admin_view_model.dart' as _i609;
import 'package:weaver/features/admin/user_admin_view_model.dart' as _i486;
import 'package:weaver/features/auth/auth_view_model.dart' as _i6;
import 'package:weaver/features/auth/data/api_auth_repository.dart' as _i461;
import 'package:weaver/features/auth/data/auth_repository.dart' as _i899;
import 'package:weaver/features/auth/data/auth_session_store.dart' as _i605;
import 'package:weaver/features/auth/data/flutter_secure_token_store.dart'
    as _i347;
import 'package:weaver/features/auth/data/secure_token_store.dart' as _i54;
import 'package:weaver/features/board/board_view_model.dart' as _i314;
import 'package:weaver/features/board/data/api_board_repository.dart' as _i436;
import 'package:weaver/features/board/data/board_repository.dart' as _i522;
import 'package:weaver/features/board/repeating_items_view_model.dart' as _i87;
import 'package:weaver/features/work_item_detail/data/api_work_item_detail_repository.dart'
    as _i335;
import 'package:weaver/features/work_item_detail/data/work_item_detail_repository.dart'
    as _i982;
import 'package:weaver/features/work_item_detail/repeat_settings_view_model.dart'
    as _i60;
import 'package:weaver/features/work_item_detail/work_item_detail_view_model.dart'
    as _i194;
import 'package:weaver/shared/data/api_layer_repository.dart' as _i284;
import 'package:weaver/shared/data/api_status_repository.dart' as _i209;
import 'package:weaver/shared/data/api_user_directory_repository.dart' as _i982;
import 'package:weaver/shared/data/current_user.dart' as _i875;
import 'package:weaver/shared/data/layer_repository.dart' as _i0;
import 'package:weaver/shared/data/status_repository.dart' as _i770;
import 'package:weaver/shared/data/user_directory_repository.dart' as _i562;
import 'package:weaver/shared/recurrence/data/api_recurrence_repository.dart'
    as _i919;
import 'package:weaver/shared/recurrence/data/recurrence_repository.dart'
    as _i65;

extension GetItInjectableX on _i174.GetIt {
  // initializes the registration of main-scope dependencies inside of GetIt
  _i174.GetIt init({
    String? environment,
    _i526.EnvironmentFilter? environmentFilter,
  }) {
    final gh = _i526.GetItHelper(this, environment, environmentFilter);
    final networkModule = _$NetworkModule();
    gh.lazySingleton<_i54.SecureTokenStore>(
      () => _i347.FlutterSecureTokenStore(),
    );
    gh.lazySingleton<_i936.ServerOriginStore>(
      () => _i781.FlutterSecureServerOriginStore(),
    );
    gh.lazySingleton<_i519.Client>(
      () => networkModule.rawHttpClient,
      instanceName: 'rawHttpClient',
    );
    gh.lazySingleton<_i577.ServerSettings>(
      () => _i577.ServerSettings(gh<_i936.ServerOriginStore>()),
    );
    gh.factory<_i202.ApiEndpoint>(
      () => networkModule.apiEndpoint(gh<_i577.ServerSettings>()),
    );
    gh.lazySingleton<_i899.AuthRepository>(
      () => _i461.ApiAuthRepository(
        gh<_i519.Client>(instanceName: 'rawHttpClient'),
        gh<_i202.ApiEndpoint>(),
      ),
    );
    gh.lazySingleton<_i605.AuthSessionStore>(
      () => _i605.AuthSessionStore(
        gh<_i899.AuthRepository>(),
        gh<_i54.SecureTokenStore>(),
      ),
    );
    gh.factory<_i875.CurrentUser>(
      () => networkModule.currentUser(gh<_i605.AuthSessionStore>()),
    );
    gh.factory<_i6.AuthViewModel>(
      () => _i6.AuthViewModel(
        gh<_i899.AuthRepository>(),
        gh<_i605.AuthSessionStore>(),
        gh<_i577.ServerSettings>(),
      ),
    );
    gh.lazySingleton<_i519.Client>(
      () => networkModule.httpClient(
        gh<_i519.Client>(instanceName: 'rawHttpClient'),
        gh<_i605.AuthSessionStore>(),
      ),
    );
    gh.lazySingleton<_i0.LayerRepository>(
      () =>
          _i284.ApiLayerRepository(gh<_i519.Client>(), gh<_i202.ApiEndpoint>()),
    );
    gh.lazySingleton<_i770.StatusRepository>(
      () => _i209.ApiStatusRepository(
        gh<_i519.Client>(),
        gh<_i202.ApiEndpoint>(),
      ),
    );
    gh.lazySingleton<_i377.LookupAdminRepository>(
      () => _i427.ApiLookupAdminRepository(
        gh<_i519.Client>(),
        gh<_i202.ApiEndpoint>(),
        gh<_i0.LayerRepository>(),
      ),
    );
    gh.lazySingleton<_i224.StatusAdminRepository>(
      () => _i550.ApiStatusAdminRepository(
        gh<_i519.Client>(),
        gh<_i202.ApiEndpoint>(),
        gh<_i770.StatusRepository>(),
      ),
    );
    gh.factoryParam<_i281.LookupAdminViewModel, _i54.LookupKind, dynamic>(
      (kind, _) =>
          _i281.LookupAdminViewModel(gh<_i377.LookupAdminRepository>(), kind),
    );
    gh.factory<_i609.StatusAdminViewModel>(
      () => _i609.StatusAdminViewModel(gh<_i224.StatusAdminRepository>()),
    );
    gh.lazySingleton<_i562.UserDirectoryRepository>(
      () => _i982.ApiUserDirectoryRepository(
        gh<_i519.Client>(),
        gh<_i202.ApiEndpoint>(),
      ),
    );
    gh.lazySingleton<_i982.WorkItemDetailRepository>(
      () => _i335.ApiWorkItemDetailRepository(
        gh<_i519.Client>(),
        gh<_i202.ApiEndpoint>(),
        gh<_i770.StatusRepository>(),
        gh<_i0.LayerRepository>(),
        gh<_i562.UserDirectoryRepository>(),
      ),
    );
    gh.lazySingleton<_i65.RecurrenceRepository>(
      () => _i919.ApiRecurrenceRepository(
        gh<_i519.Client>(),
        gh<_i202.ApiEndpoint>(),
      ),
    );
    gh.factory<_i87.RepeatingItemsViewModel>(
      () => _i87.RepeatingItemsViewModel(gh<_i65.RecurrenceRepository>()),
    );
    gh.factory<_i60.RepeatSettingsViewModel>(
      () => _i60.RepeatSettingsViewModel(gh<_i65.RecurrenceRepository>()),
    );
    gh.lazySingleton<_i522.BoardRepository>(
      () => _i436.ApiBoardRepository(
        gh<_i519.Client>(),
        gh<_i202.ApiEndpoint>(),
        gh<_i770.StatusRepository>(),
        gh<_i0.LayerRepository>(),
        gh<_i562.UserDirectoryRepository>(),
      ),
    );
    gh.factory<_i314.BoardViewModel>(
      () => _i314.BoardViewModel(gh<_i522.BoardRepository>()),
    );
    gh.lazySingleton<_i290.UserAdminRepository>(
      () => _i756.ApiUserAdminRepository(
        gh<_i519.Client>(),
        gh<_i202.ApiEndpoint>(),
        gh<_i562.UserDirectoryRepository>(),
      ),
    );
    gh.factory<_i486.UserAdminViewModel>(
      () => _i486.UserAdminViewModel(
        gh<_i290.UserAdminRepository>(),
        gh<_i875.CurrentUser>(),
      ),
    );
    gh.factory<_i194.WorkItemDetailViewModel>(
      () => _i194.WorkItemDetailViewModel(
        gh<_i982.WorkItemDetailRepository>(),
        gh<_i875.CurrentUser>(),
      ),
    );
    return this;
  }
}

class _$NetworkModule extends _i561.NetworkModule {}
