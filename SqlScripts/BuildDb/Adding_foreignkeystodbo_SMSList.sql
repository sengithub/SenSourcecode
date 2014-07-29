ALTER TABLE [dbo].[SMSList] ADD CONSTRAINT [FK_SMSList_SMSGroups] FOREIGN KEY ([SendGroupID]) REFERENCES [dbo].[SMSGroups] ([ID])
ALTER TABLE [dbo].[SMSList] ADD CONSTRAINT [FK_SMSList_People] FOREIGN KEY ([SenderID]) REFERENCES [dbo].[People] ([PeopleId])
GO
IF @@ERROR<>0 AND @@TRANCOUNT>0 ROLLBACK TRANSACTION
GO
IF @@TRANCOUNT=0 BEGIN INSERT INTO #tmpErrors (Error) SELECT 1 BEGIN TRANSACTION END
GO