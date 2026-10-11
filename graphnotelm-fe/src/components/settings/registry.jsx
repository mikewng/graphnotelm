import AccountSettings from './sections/AccountSettings'
import LlmMcpSettings from './sections/LlmMcpSettings'
import MiscSettings from './sections/MiscSettings'

// ── Section registry ─────────────────────────────────────────────────────────
//
// This is the only place the settings modal learns about a topic. To add one:
//
//   1. Drop a component in ./sections that renders its own controls, composed
//      from ./primitives. It owns its own loading, saving and error state.
//   2. Append an entry below.
//
// Entry shape:
//   id           unique slug, also the nav key
//   label        nav label
//   description  subtitle shown at the top of the pane
//   icon         React node rendered in the nav rail
//   Component    the section body
//   isAvailable  optional () => boolean; hides the section when it returns false

const IconAccount = (
  <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" strokeLinejoin="round">
    <circle cx="8" cy="5.5" r="2.75" />
    <path d="M2.75 13.5c.6-2.6 2.7-4 5.25-4s4.65 1.4 5.25 4" />
  </svg>
)

const IconLlm = (
  <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" strokeLinejoin="round">
    <rect x="4" y="4" width="8" height="8" rx="1.5" />
    <path d="M6.5 1.75v2.25M9.5 1.75v2.25M6.5 12v2.25M9.5 12v2.25M1.75 6.5H4M1.75 9.5H4M12 6.5h2.25M12 9.5h2.25" />
  </svg>
)

const IconMisc = (
  <svg viewBox="0 0 16 16" fill="none" stroke="currentColor" strokeWidth="1.3" strokeLinecap="round" strokeLinejoin="round">
    <path d="M2 4.5h12M2 8h12M2 11.5h12" />
    <circle cx="5.5" cy="4.5" r="1.5" fill="currentColor" stroke="none" />
    <circle cx="10.5" cy="8" r="1.5" fill="currentColor" stroke="none" />
    <circle cx="6.5" cy="11.5" r="1.5" fill="currentColor" stroke="none" />
  </svg>
)

export const SETTINGS_SECTIONS = [
  {
    id: 'account',
    label: 'Account',
    description: 'Your profile details and how long this session stays signed in.',
    icon: IconAccount,
    Component: AccountSettings,
  },
  {
    id: 'llms-mcp',
    label: 'LLMs & MCP',
    description: 'Which model answers, where its key comes from, and how MCP clients connect.',
    icon: IconLlm,
    Component: LlmMcpSettings,
  },
  {
    id: 'misc',
    label: 'Misc',
    description: 'Local app state and environment details.',
    icon: IconMisc,
    Component: MiscSettings,
  },
]

export function getAvailableSections() {
  return SETTINGS_SECTIONS.filter(s => !s.isAvailable || s.isAvailable())
}
