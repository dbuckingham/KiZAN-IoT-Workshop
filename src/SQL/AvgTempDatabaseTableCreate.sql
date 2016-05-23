USE [AvgTemp]
GO

/****** Object:  Table [dbo].[AvgHoppingByDeviceTemperature]    Script Date: 5/23/2016 8:14:42 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

SET ANSI_PADDING ON
GO

CREATE TABLE [dbo].[AvgHoppingByDeviceTemperature](
	[DeviceId] [varchar](25) NOT NULL,
	[Timestamp] [datetime] NOT NULL,
	[TempC] [float] NOT NULL,
	[TempF] [float] NOT NULL
)

GO

SET ANSI_PADDING OFF
GO

/****** Object:  Table [dbo].[AvgHoppingTemperature]    Script Date: 5/23/2016 8:14:42 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[AvgHoppingTemperature](
	[Timestamp] [datetime] NOT NULL,
	[TempC] [float] NOT NULL,
	[TempF] [float] NOT NULL
)

GO

/****** Object:  Table [dbo].[AvgTumblingByDeviceTemperature]    Script Date: 5/23/2016 8:14:43 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

SET ANSI_PADDING ON
GO

CREATE TABLE [dbo].[AvgTumblingByDeviceTemperature](
	[DeviceId] [varchar](25) NOT NULL,
	[Timestamp] [datetime] NOT NULL,
	[TempC] [float] NOT NULL,
	[TempF] [float] NOT NULL
)

GO

SET ANSI_PADDING OFF
GO

/****** Object:  Table [dbo].[AvgTumblingTemperature]    Script Date: 5/23/2016 8:14:43 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE TABLE [dbo].[AvgTumblingTemperature](
	[Timestamp] [datetime] NOT NULL,
	[TempC] [float] NOT NULL,
	[TempF] [float] NOT NULL
)

GO

/****** Object:  Table [dbo].[Temperature]    Script Date: 5/23/2016 8:14:44 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

SET ANSI_PADDING ON
GO

CREATE TABLE [dbo].[Temperature](
	[DeviceId] [varchar](25) NOT NULL,
	[Timestamp] [datetime] NOT NULL,
	[TempC] [float] NOT NULL,
	[TempF] [float] NOT NULL
)

GO

SET ANSI_PADDING OFF
GO

ALTER TABLE [dbo].[AvgHoppingByDeviceTemperature] ADD  DEFAULT ('Unknown') FOR [DeviceId]
GO

ALTER TABLE [dbo].[AvgHoppingByDeviceTemperature] ADD  DEFAULT (getdate()) FOR [Timestamp]
GO

ALTER TABLE [dbo].[AvgHoppingByDeviceTemperature] ADD  DEFAULT ((0)) FOR [TempC]
GO

ALTER TABLE [dbo].[AvgHoppingByDeviceTemperature] ADD  DEFAULT ((0)) FOR [TempF]
GO

ALTER TABLE [dbo].[AvgHoppingTemperature] ADD  DEFAULT (getdate()) FOR [Timestamp]
GO

ALTER TABLE [dbo].[AvgHoppingTemperature] ADD  DEFAULT ((0)) FOR [TempC]
GO

ALTER TABLE [dbo].[AvgHoppingTemperature] ADD  DEFAULT ((0)) FOR [TempF]
GO

ALTER TABLE [dbo].[AvgTumblingByDeviceTemperature] ADD  DEFAULT ('Unknown') FOR [DeviceId]
GO

ALTER TABLE [dbo].[AvgTumblingByDeviceTemperature] ADD  DEFAULT (getdate()) FOR [Timestamp]
GO

ALTER TABLE [dbo].[AvgTumblingByDeviceTemperature] ADD  DEFAULT ((0)) FOR [TempC]
GO

ALTER TABLE [dbo].[AvgTumblingByDeviceTemperature] ADD  DEFAULT ((0)) FOR [TempF]
GO

ALTER TABLE [dbo].[AvgTumblingTemperature] ADD  DEFAULT (getdate()) FOR [Timestamp]
GO

ALTER TABLE [dbo].[AvgTumblingTemperature] ADD  DEFAULT ((0)) FOR [TempC]
GO

ALTER TABLE [dbo].[AvgTumblingTemperature] ADD  DEFAULT ((0)) FOR [TempF]
GO

ALTER TABLE [dbo].[Temperature] ADD  DEFAULT ('Unknown') FOR [DeviceId]
GO

ALTER TABLE [dbo].[Temperature] ADD  DEFAULT (getdate()) FOR [Timestamp]
GO

ALTER TABLE [dbo].[Temperature] ADD  DEFAULT ((0)) FOR [TempC]
GO

ALTER TABLE [dbo].[Temperature] ADD  DEFAULT ((0)) FOR [TempF]
GO


