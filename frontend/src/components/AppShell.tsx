// App shell per the Stitch design: fixed top bar (logo left, nav, profile right).
interface Props {
  active: "chat" | "exam" | "review";
  children: React.ReactNode;
}

export default function AppShell({ active, children }: Props) {
  const navLink = (key: string, label: string) => (
    <a
      className={
        "flex items-center h-full px-base text-label-md font-label-md transition-colors " +
        (active === key
          ? "text-primary font-bold border-b-2 border-primary"
          : "text-on-surface-variant hover:text-primary")
      }
      data-active={active === key ? "true" : undefined}
    >
      {label}
    </a>
  );

  return (
    <div className="min-h-screen bg-background font-body-md text-on-background">
      <header className="fixed top-0 w-full z-50 bg-surface/90 backdrop-blur-xl shadow-[0_1px_8px_rgba(0,0,0,0.04)]">
        <div className="h-20 max-w-container-max mx-auto px-lg lg:px-xxl flex items-center justify-between">
          <div className="flex items-center gap-md">
            <div className="w-8 h-8 rounded-lg bg-primary-container text-on-primary grid place-items-center text-lg shadow-sm">◆</div>
            <span className="font-headline-md text-headline-md text-primary tracking-tight">ezCert</span>
          </div>
          <nav className="hidden md:flex items-center gap-xl h-full">
            {navLink("chat", "Chat")}
            {navLink("exam", "Exam")}
            {navLink("review", "Review")}
          </nav>
          <div className="flex items-center gap-md">
            <div className="ml-base border-l border-outline-variant pl-md flex items-center gap-sm">
              <div className="w-9 h-9 rounded-full bg-secondary-container grid place-items-center font-label-md text-on-secondary-container">
                G
              </div>
              <span className="hidden sm:block text-label-md text-on-surface-variant">Guest</span>
            </div>
          </div>
        </div>
      </header>
      <main className="pt-20">{children}</main>
    </div>
  );
}
