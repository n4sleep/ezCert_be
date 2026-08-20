---
name: ezCert ExamGenius v2
status: final
created: '2026-08-12'
updated: '2026-08-12'
sources:
  - 'docs/design/design-contract-examgenius-v2.md'
  - 'imports/stitch-DESIGN.md'
  - 'imports/stitch-chat-request.html'
  - 'imports/stitch-taking-exam.html'
  - 'imports/stitch-results.html'
---

colors:
  surface: '#f8f9ff'
  surface-container-lowest: '#ffffff'
  surface-container-low: '#eff4ff'
  surface-container: '#e6eeff'
  surface-container-high: '#dce9ff'
  surface-container-highest: '#d5e3fc'
  on-surface: '#0d1c2e'
  on-surface-variant: '#464555'
  primary: '#4f46e5'
  primary-container: '#e2dfff'
  on-primary: '#ffffff'
  secondary: '#575e72'
  secondary-container: '#dbe2fa'
  on-secondary-container: '#5d6478'
  error: '#ba1a1a'
  error-container: '#ffdad6'
  on-error-container: '#93000a'
  success: '#10b981'
  success-strong: '#1b5e20'
  success-soft: '#e8f5e9'
  danger: '#c62828'
  danger-soft: '#ffebee'
  outline: '#777587'
  outline-variant: '#c7c4d8'

typography:
  display-lg: { fontFamily: Inter, fontSize: 48px, fontWeight: 700, lineHeight: 56px, letterSpacing: -0.02em }
  headline-lg: { fontFamily: Inter, fontSize: 32px, fontWeight: 600, lineHeight: 40px, letterSpacing: -0.01em }
  headline-md: { fontFamily: Inter, fontSize: 24px, fontWeight: 600, lineHeight: 32px }
  body-lg:     { fontFamily: Inter, fontSize: 18px, fontWeight: 400, lineHeight: 28px }
  body-md:     { fontFamily: Inter, fontSize: 16px, fontWeight: 400, lineHeight: 24px }
  body-sm:     { fontFamily: Inter, fontSize: 14px, fontWeight: 400, lineHeight: 20px }
  label-md:    { fontFamily: Inter, fontSize: 14px, fontWeight: 500, lineHeight: 20px, letterSpacing: 0.01em }
  label-caps:  { fontFamily: Inter, fontSize: 12px, fontWeight: 600, lineHeight: 16px, letterSpacing: 0.05em }

rounded:
  sm: 0.5rem
  DEFAULT: 0.75rem
  md: 1rem
  lg: 1.25rem
  xl: 1.5rem
  full: 9999px

spacing:
  base: 4px
  xs: 4px
  sm: 8px
  md: 16px
  lg: 24px
  xl: 40px
  xxl: 64px
  container-max: 1280px
  gutter: 24px

components:
  button:
    primary: { bg: '#4f46e5', text: '#ffffff', radius: 1rem, shadow: '0 4px 14px rgba(79,70,229,0.2)' }
    secondary: { bg: '#dbe2fa', text: '#3525cd', radius: 1rem }
    ghost: { bg: transparent, text: '#575e72', radius: 1rem }
    success: { bg: '#10b981', text: '#ffffff', radius: 1rem }
    danger: { bg: '#ba1a1a', text: '#ffffff', radius: 1rem }
  card: { bg: '#ffffff', radius: 1.5rem, shadow: '0 4px 20px rgba(0,0,0,0.05)', border: '1px solid #e2e8f0' }
  input: { bg: '#ffffff', border: '#cbd5e1', radius: 1rem, focus-ring: '3px rgba(79,70,229,0.25)' }
  exam-option-selected: { bg: '#dbe2fa', ring: '#4f46e5' }

# Brand & Style

ezCert is a premium EdTech product: modern, clean, and professional â€” high-end productivity tool, not a classroom app. The visual language is adopted from the Stitch ExamGenius prototype (`imports/stitch-DESIGN.md`).

- **Clarity of thought:** generous whitespace, restricted palette, low cognitive load.
- **Precision:** rigorous grid and sharp Inter typography build trust in assessment data.
- **Encouragement:** soft rounded corners and subtle transitions make testing feel supportive, not intimidating.

# Colors

- **Primary indigo (`#4F46E5`):** main CTAs, active states, progress indicators, brand.
- **Soft blue surfaces (`#E6EEFF` family):** backgrounds and subtle highlights that differentiate from primary.
- **Slate text (`#0D1C2E` / `#464555`):** high readability without harsh black.
- **Canvas (`#F8F9FF`):** main background so white cards pop.
- **Semantic:** emerald (`#10B981` / `#1B5E20`) for passed/correct; rose/red (`#C62828` / `#BA1A1A`) for failed/incorrect.


# Typography

Inter exclusively â€” exceptional digital legibility, neutral professional tone.

- display-lg for the results score and hero moments.
- headline-lg for exam titles; headline-md for card titles.
- body-md with relaxed line height for long-form questions.
- label-caps for small meta: "Time Remaining", "Question 1 of 10", "3 days left".

# Layout & Spacing

- **Fixed-fluid hybrid:** main content capped at 1280px; sidebars fluid.
- **Margins:** 24px mobile, 64px desktop.
- **Rhythm:** multiples of 8px.
- Three screens (Builder / Exam / Results) share one AppShell: fixed top bar + optional sidebar on Builder only.

# Elevation & Depth

- **Ambient soft depth** instead of heavy shadows.
- Cards: white on #F8F9FF,  0 4px 20px rgba(0,0,0,0.05), 1px #E2E8F0 border.
- Interactive elevation (selected answer):  0 10px 25px rgba(79,70,229,0.1).
- Fixed top bar: backdrop blur (12px) at 90% opacity.

# Shapes

- **Cards, modals, sections:** rounded-xl (1.5rem).
- **Buttons, inputs:** rounded-lg (1rem).
- **Chips/tags:** pill (rounded-full).

# Components

- **Button primary:** solid indigo, white text, rounded-lg, soft indigo shadow; hover lifts slightly.
- **Button secondary:** soft blue bg, indigo text, no border.
- **Button ghost:** transparent, slate text, indigo on hover.
- **Button success (Submit Exam):** emerald #10B981, white text.
- **Button danger (Delete exam):** rose/red #BA1A1A, white text; confirmation required before it acts.
- **Input:** white, 1px slate border; focus = indigo border + 3px soft indigo ring.
- **Cards:** white, rounded-xl, generous padding (24â€“32px).
- **Chips:** pill-shaped, low-saturation bg from indigo or status colors.
- **Progress bars:** 8px, rounded; indigo for overall, emerald for correct segments.
- **Exam option:** bordered option card; selected = soft blue bg + indigo ring + filled radio; hover = slight shadow.
- **Exam Card (chat):** white card with soft shadow; generated badge; title; meta pills (count, minutes); Start button; expiry badge.

# Do's and Don'ts

- **Do:** keep the canvas light, cards white, one indigo primary per view; use emerald/rose only for status; animate exam-card and score-ring arrivals; respect `prefers-reduced-motion` (no pulse or ring animation when set).
- **Don't:** add dark mode this round; hide scrollbars globally; use the Stitch Tailwind CDN or remote image assets in the React build; reuse the "Start Exam Link" wording (use "Start exam"); show search/notification icons with no purpose.

# Motion

- **Exam Card arrival:** slide-up + fade, ~300ms ease-out, once.
- **Score ring:** animates to the final percentage on Results mount (~900ms ease-out).
- **Pulse/typing indicator:** soft opacity pulse while a job is queued/running.
- All motion disabled under `prefers-reduced-motion: reduce`.
