$mainWindowPath = "Views\MainWindow.axaml"
$settingsViewPath = "Views\SettingsView.axaml"

$template = @'
			<Setter Property="Template">
				<ControlTemplate>
					<Border Background="{TemplateBinding Background}"
							BorderBrush="{TemplateBinding BorderBrush}"
							BorderThickness="{TemplateBinding BorderThickness}"
							CornerRadius="{TemplateBinding CornerRadius}"
							Padding="{TemplateBinding Padding}">
						<ContentPresenter Content="{TemplateBinding Content}"
										  Foreground="{TemplateBinding Foreground}"
										  HorizontalContentAlignment="Center"
										  VerticalContentAlignment="Center"/>
					</Border>
				</ControlTemplate>
			</Setter>
'@

# Add template to Button.download
$content = Get-Content $mainWindowPath -Raw
$content = $content -replace '(<Style Selector="Button\.download">[\s\S]*?<DropShadowEffect[^>]*/>[\s\S]*?</Setter>)', "`$1`n$template"
$content | Set-Content $mainWindowPath -NoNewline

Write-Host "Added templates to all button styles"
