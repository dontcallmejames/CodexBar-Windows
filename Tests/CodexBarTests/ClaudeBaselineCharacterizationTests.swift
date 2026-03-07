import CodexBarCore
import Foundation
import Testing

@Suite
struct ClaudeBaselineCharacterizationTests {
    private func makeContext(
        runtime: ProviderRuntime,
        sourceMode: ProviderSourceMode,
        settings: ProviderSettingsSnapshot? = nil) -> ProviderFetchContext
    {
        let env: [String: String] = [:]
        let browserDetection = BrowserDetection(cacheTTL: 0)
        return ProviderFetchContext(
            runtime: runtime,
            sourceMode: sourceMode,
            includeCredits: false,
            webTimeout: 1,
            webDebugDumpHTML: false,
            verbose: false,
            env: env,
            settings: settings,
            fetcher: UsageFetcher(environment: env),
            claudeFetcher: ClaudeUsageFetcher(browserDetection: browserDetection),
            browserDetection: browserDetection)
    }

    private func strategyIDs(
        runtime: ProviderRuntime,
        sourceMode: ProviderSourceMode,
        settings: ProviderSettingsSnapshot? = nil) async -> [String]
    {
        let descriptor = ProviderDescriptorRegistry.descriptor(for: .claude)
        let context = self.makeContext(runtime: runtime, sourceMode: sourceMode, settings: settings)
        let strategies = await descriptor.fetchPlan.pipeline.resolveStrategies(context)
        return strategies.map(\.id)
    }

    @Test
    func appAutoPipelineOrder_isOAuthThenCLIThenWeb() async {
        let strategyIDs = await self.strategyIDs(runtime: .app, sourceMode: .auto)
        #expect(strategyIDs == ["claude.oauth", "claude.cli", "claude.web"])
    }

    @Test
    func cliAutoPipelineOrder_isWebThenCLI() async {
        let strategyIDs = await self.strategyIDs(runtime: .cli, sourceMode: .auto)
        #expect(strategyIDs == ["claude.web", "claude.cli"])
    }

    @Test(arguments: [
        (ProviderSourceMode.oauth, "claude.oauth"),
        (ProviderSourceMode.cli, "claude.cli"),
        (ProviderSourceMode.web, "claude.web"),
    ])
    func explicitModesResolveSingleClaudeStrategy(sourceMode: ProviderSourceMode, expectedStrategyID: String) async {
        let strategyIDs = await self.strategyIDs(runtime: .app, sourceMode: sourceMode)
        #expect(strategyIDs == [expectedStrategyID])
    }

    @Test(arguments: [
        (ProviderSourceMode.oauth, "claude.oauth"),
        (ProviderSourceMode.cli, "claude.cli"),
        (ProviderSourceMode.web, "claude.web"),
    ])
    func cliExplicitModesResolveSingleClaudeStrategy(sourceMode: ProviderSourceMode, expectedStrategyID: String) async {
        let strategyIDs = await self.strategyIDs(runtime: .cli, sourceMode: sourceMode)
        #expect(strategyIDs == [expectedStrategyID])
    }

    @Test
    func claudeOAuthTokenHeuristics_acceptRawAndBearerInputs() {
        #expect(TokenAccountSupportCatalog.isClaudeOAuthToken("sk-ant-oat-test-token"))
        #expect(TokenAccountSupportCatalog.isClaudeOAuthToken("Bearer sk-ant-oat-test-token"))
    }

    @Test
    func claudeOAuthTokenHeuristics_rejectCookieShapedInputs() {
        #expect(!TokenAccountSupportCatalog.isClaudeOAuthToken("sessionKey=sk-ant-session"))
        #expect(!TokenAccountSupportCatalog.isClaudeOAuthToken("Cookie: sessionKey=sk-ant-session; foo=bar"))
    }
}
