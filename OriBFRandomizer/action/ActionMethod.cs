public static class ActionMethodExt
{
	public static void PerformInstantly(this ActionMethod _this, IContext context)
	{
		_this.Perform(context);
	}

}
